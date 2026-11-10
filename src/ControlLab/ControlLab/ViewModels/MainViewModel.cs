using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Controls;
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
    [RelayCommand] private Task LedOnAsync() => Session.SendAsync("led", true);
    [RelayCommand] private Task LedOffAsync() => Session.SendAsync("led", false);
    [RelayCommand] private Task PingAsync() => Session.SendAsync("ping");
    [RelayCommand] private void ClearLogs() => Session.Logs.Clear();
}
