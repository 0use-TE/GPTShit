using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
namespace ControlLab.Services;

public partial class SimulationSession(LocalWebServer server) : ObservableObject
{
    public Uri Address => server.Address;
    public Func<string, Task<string?>>? ExecuteScript { get; set; }
    public bool IsDarkTheme { get; private set; } = true;
    public ObservableCollection<string> Logs { get; } = [];
    public IReadOnlyList<ThermalSample> Samples { get; private set; } = Array.Empty<ThermalSample>();
    private readonly List<ThermalSample> _samples = [];
    private readonly Dictionary<string, TaskCompletionSource<bool>> _pending = [];
    private int _sequence;
    [ObservableProperty] private string _status = "正在连接仿真";
    [ObservableProperty] private string _connectionLabel = "连接中";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isHeating;
    [ObservableProperty] private bool _doorOpen;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private double _temperature = 25;
    [ObservableProperty] private double _actualTarget = 50;
    [ObservableProperty] private double _heaterOutput;
    [ObservableProperty] private int _ackCount;
    [ObservableProperty] private string _fault = "";
    public string ControlAction => IsHeating ? "停止控温" : "开始控温";
    public string TemperatureText => Temperature.ToString("F1", CultureInfo.InvariantCulture) + " °C";
    public string OutputText => "加热 " + HeaterOutput.ToString("F0", CultureInfo.InvariantCulture) + "%";
    public string DoorText => DoorOpen ? "门已打开" : "开门 5 秒";
    public string SafetyText => Fault switch { "Sensor fault" => "传感器故障，已停止加热", "Overtemperature" => "超温保护，已停止加热", _ => Fault };
    partial void OnIsHeatingChanged(bool value) => OnPropertyChanged(nameof(ControlAction));
    partial void OnTemperatureChanged(double value) => OnPropertyChanged(nameof(TemperatureText));
    partial void OnHeaterOutputChanged(double value) => OnPropertyChanged(nameof(OutputText));
    partial void OnDoorOpenChanged(bool value) => OnPropertyChanged(nameof(DoorText));
    partial void OnFaultChanged(string value) => OnPropertyChanged(nameof(SafetyText));

    public void Receive(string body)
    {
        try
        {
            var m = JsonSerializer.Deserialize(body, ProtocolJsonContext.Default.BridgeMessage);
            if (m is null) return;
            switch (m.Type)
            {
                case "connected": IsConnected = true; HasError = false; ConnectionLabel = "已连接"; Status = "仿真已连接"; break;
                case "ready":
                    IsRunning = true; HasError = false; ConnectionLabel = "运行中"; Status = "ESP32 已就绪";
                    ClearSamples(); break;
                case "loading": ResetConnection(); break;
                case "starting": ConnectionLabel = "启动中"; Status = m.Message ?? "启动中"; break;
                case "ack":
                    AckCount++; HasError = false;
                    if (m.Id is { } id && _pending.Remove(id, out var pending)) pending.TrySetResult(true);
                    break;
                case "error":
                    HasError = true; ConnectionLabel = "出现错误"; Status = m.Message ?? "通信错误";
                    foreach (var request in _pending.Values) request.TrySetResult(false);
                    _pending.Clear(); break;
            }
            if (m.Temperature is { } t && double.IsFinite(t)) Temperature = t;
            if (m.Target is { } target) ActualTarget = target;
            if (m.Output is { } output) HeaterOutput = output;
            if (m.Enabled is { } enabled) IsHeating = enabled;
            if (m.Door is { } door) DoorOpen = door;
            if (m.Fault is { } fault)
            {
                var wasDeviceFault = Fault.Length > 0;
                Fault = fault;
                if (fault.Length > 0) { HasError = true; ConnectionLabel = "保护停机"; Status = SafetyText; }
                else if (wasDeviceFault) { HasError = false; ConnectionLabel = "运行中"; }
            }
            if (m.Type == "telemetry" && m.Uptime is { } ms)
            {
                _samples.Add(new(ms / 1000.0, Temperature, ActualTarget, HeaterOutput, DoorOpen, IsHeating));
                if (_samples.Count > 7200) _samples.RemoveAt(0);
                Samples = _samples.ToArray(); OnPropertyChanged(nameof(Samples));
            }
            // Telemetry goes to the curve; logs retain commands and device errors.
            if (m.Type is not "telemetry" and not "serial") AddLog($"{m.Type.ToUpperInvariant()}  {m.Message ?? body}");
            else if (m.Type == "serial" && m.Message is { } text && !text.StartsWith('{')) AddLog(text);
        }
        catch (JsonException error) { AddLog($"消息格式错误：{error.Message}"); }
    }

    public async Task StartAsync() => await InvokeAsync("window.controlLab.start()");
    public async Task SetThemeAsync(bool isDark)
    {
        IsDarkTheme = isDark;
        if (ExecuteScript is not null) await InvokeAsync($"window.controlLab?.setTheme({(isDark ? "true" : "false")})");
    }
    public async Task<bool> SendAsync(DeviceCommand command)
    {
        if (!IsRunning) { Status = "请先启动仿真"; return false; }
        var id = (++_sequence).ToString(CultureInfo.InvariantCulture);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        try
        {
            var json = JsonSerializer.Serialize(command with { Id = id }, ProtocolJsonContext.Default.DeviceCommand);
            if (!await InvokeAsync($"window.controlLab.send({json})")) return false;
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
        }
        catch (TimeoutException) { HasError = true; Status = "下位机确认超时"; AddLog(Status); return false; }
        finally { _pending.Remove(id); }
    }
    public async Task ReconnectAsync()
    {
        // Run reload before resetting the delegate; navigation events invalidate it afterwards.
        await InvokeAsync("window.controlLab.reconnect()");
        ResetConnection();
    }
    private void ResetConnection()
    {
        IsConnected = false; IsRunning = false; IsHeating = false; HasError = false;
        DoorOpen = false; Fault = ""; HeaterOutput = 0; ConnectionLabel = "连接中";
        foreach (var request in _pending.Values) request.TrySetResult(false);
        _pending.Clear();
    }
    private async Task<bool> InvokeAsync(string script)
    {
        try
        {
            if (ExecuteScript is null) throw new InvalidOperationException("仿真页面尚未就绪");
            await ExecuteScript(script); return true;
        }
        catch (Exception error) { HasError = true; Status = error.Message; AddLog($"ERROR  {error.Message}"); return false; }
    }
    public void ClearSamples() { _samples.Clear(); Samples = Array.Empty<ThermalSample>(); OnPropertyChanged(nameof(Samples)); }
    public void AddLog(string text)
    {
        Logs.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}");
        while (Logs.Count > 200) Logs.RemoveAt(Logs.Count - 1);
    }
}
