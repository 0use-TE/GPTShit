using System;
using System.Collections.ObjectModel;
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
    [ObservableProperty] private string _status = "正在连接 Wokwi…";
    [ObservableProperty] private string _connectionLabel = "连接中";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _ledState = "LED 未就绪";
    [ObservableProperty] private string _uptime = "—";
    [ObservableProperty] private int _ackCount;
    [ObservableProperty] private bool _hasError;
    private int _sequence;
    public void Receive(string body)
    {
        try
        {
            var message = JsonSerializer.Deserialize(body, ProtocolJsonContext.Default.BridgeMessage);
            if (message is null) return;
            ConnectionLabel = message.Type switch
            {
                "connected" => "已连接", "ready" or "ack" => "运行中",
                "loading" => "连接中", "starting" => "启动中", "error" => "出现错误", _ => ConnectionLabel
            };
            switch (message.Type)
            {
                case "connected": IsConnected = true; HasError = false; Status = message.Message ?? "已连接"; break;
                case "ready": IsRunning = true; HasError = false; Status = "下位机运行中"; break;
                case "loading": IsConnected = false; IsRunning = false; Status = message.Message ?? "加载中"; break;
                case "starting": Status = message.Message ?? "启动中"; break;
                case "ack": AckCount++; Status = $"下位机已确认命令 #{message.Id}"; break;
                case "error": HasError = true; Status = message.Message ?? "通信错误"; break;
            }
            if (message.Led is { } led) LedState = led ? "LED 已点亮" : "LED 已熄灭";
            if (message.Uptime is { } ms) Uptime = $"{ms / 1000.0:F1} s";
            if (message.Type != "heartbeat") AddLog($"{message.Type.ToUpperInvariant()}  {message.Message ?? body}");
        }
        catch (JsonException error) { AddLog($"无法解析桥接消息：{error.Message}"); }
    }
    public async Task StartAsync() => await InvokeAsync("window.controlLab.start()");
    public async Task SetThemeAsync(bool isDark)
    {
        IsDarkTheme = isDark;
        if (ExecuteScript is not null)
            await InvokeAsync($"window.controlLab?.setTheme({(isDark ? "true" : "false")})");
    }
    public async Task SendAsync(string command, bool? value = null)
    {
        if (!IsRunning) { Status = "请先启动仿真并等待下位机就绪"; return; }
        var json = JsonSerializer.Serialize(new DeviceCommand(command, (++_sequence).ToString(), value), ProtocolJsonContext.Default.DeviceCommand);
        await InvokeAsync($"window.controlLab.send({json})");
    }
    public async Task ReconnectAsync()
    {
        IsConnected = false; IsRunning = false; HasError = false;
        LedState = "LED 未就绪"; Uptime = "—"; Status = "正在重新连接…"; ConnectionLabel = "连接中";
        await InvokeAsync("window.controlLab.reconnect()");
    }
    private async Task InvokeAsync(string script)
    {
        try
        {
            if (ExecuteScript is null) throw new InvalidOperationException("WebView 尚未加载");
            await ExecuteScript(script);
        }
        catch (Exception error) { HasError = true; Status = error.Message; AddLog($"ERROR  {error.Message}"); }
    }
    public void AddLog(string text)
    {
        Logs.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}");
        while (Logs.Count > 200) Logs.RemoveAt(Logs.Count - 1);
    }
}
