using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlLab.Services;
using GOZA.Dock;

namespace ControlLab.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public SimulationSession Session { get; }
    public ObservableCollection<WorkspaceTab> ToolTabs { get; }
    public ObservableCollection<WorkspaceTab> DocumentTabs { get; }
    public ObservableCollection<WorkspaceTab> OutputTabs { get; }
    [ObservableProperty] private bool _isDarkTheme = true;
    [ObservableProperty] private WorkspaceTab? _selectedDocument;
    [ObservableProperty] private WorkspaceTab? _selectedTool;
    [ObservableProperty] private WorkspaceTab? _selectedOutput;
    [ObservableProperty] private VsCodeColorTheme _dockTheme = DockColorThemeCatalog.Create(DockColorTheme.DarkModern);
    public string ThemeAction => IsDarkTheme ? "浅色模式" : "深色模式";
    public string ThemeName => IsDarkTheme ? "Dark Modern" : "Light Modern";
    [ObservableProperty] private decimal _targetTemperature = 50m;
    [ObservableProperty] private decimal _kp = 8m;
    [ObservableProperty] private decimal _ki = 0.7m;
    [ObservableProperty] private decimal _kd = 4m;
    private readonly CircuitTab _circuitTab;
    private readonly SourceTab _sourceTab;

    public MainViewModel(SimulationSession session)
    {
        Session = session;
        ToolTabs = [new DeviceTab(this)];
        SelectedTool = ToolTabs[0];
        _circuitTab = new CircuitTab(this);
        _sourceTab = new SourceTab(this);
        DocumentTabs = [_circuitTab];
        SelectedDocument = DocumentTabs[0];
        OutputTabs = [_sourceTab, new LogTab(this)];
        SelectedOutput = OutputTabs[0];
    }

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        IsDarkTheme = !IsDarkTheme;
        Application.Current!.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        DockTheme = DockColorThemeCatalog.Create(IsDarkTheme ? DockColorTheme.DarkModern : DockColorTheme.LightModern);
        OnPropertyChanged(nameof(ThemeAction));
        OnPropertyChanged(nameof(ThemeName));
        await Session.SetThemeAsync(IsDarkTheme);
    }
    [RelayCommand] private Task StartAsync() => Session.StartAsync();
    [RelayCommand] private void ShowSource() => SelectTab(_sourceTab);
    [RelayCommand] private void ShowCircuit() => SelectTab(_circuitTab);
    private void SelectTab(WorkspaceTab tab)
    {
        if (DocumentTabs.Contains(tab)) SelectedDocument = tab;
        else if (ToolTabs.Contains(tab)) SelectedTool = tab;
        else if (OutputTabs.Contains(tab)) SelectedOutput = tab;
    }
    [RelayCommand] private Task ReconnectAsync() => Session.ReconnectAsync();
    private Task<bool> ApplyConfigurationAsync() => Session.SendAsync(new DeviceCommand("configure", "")
    { Target = (double)TargetTemperature, Kp = (double)Kp, Ki = (double)Ki, Kd = (double)Kd });
    [RelayCommand] private async Task ApplyAsync() => await ApplyConfigurationAsync();
    [RelayCommand] private async Task ToggleControlAsync()
    {
        if (Session.IsHeating)
            await Session.SendAsync(new DeviceCommand("enable", "") { Enabled = false });
        else if (await ApplyConfigurationAsync())
            await Session.SendAsync(new DeviceCommand("enable", "") { Enabled = true });
    }
    [RelayCommand] private async Task OpenDoorAsync() => await Session.SendAsync(new DeviceCommand("door", "") { Duration = 5 });
    [RelayCommand] private async Task PingAsync() => await Session.SendAsync(new DeviceCommand("ping", ""));
    [RelayCommand] private void ClearLogs() => Session.Logs.Clear();
}
