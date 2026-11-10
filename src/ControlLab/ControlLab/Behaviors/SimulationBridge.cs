using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ControlLab.Services;
namespace ControlLab.Behaviors;

public sealed class SimulationBridge : AvaloniaObject
{
    private static readonly AttachedProperty<bool> DocumentReadyProperty =
        AvaloniaProperty.RegisterAttached<SimulationBridge, NativeWebView, bool>("DocumentReady");
    public static readonly AttachedProperty<SimulationSession?> SessionProperty =
        AvaloniaProperty.RegisterAttached<SimulationBridge, NativeWebView, SimulationSession?>("Session");
    public static void SetSession(NativeWebView control, SimulationSession? value) => control.SetValue(SessionProperty, value);
    public static SimulationSession? GetSession(NativeWebView control) => control.GetValue(SessionProperty);
    static SimulationBridge() => SessionProperty.Changed.AddClassHandler<NativeWebView>((view, change) =>
    {
        view.AdapterCreated -= OnAdapterCreated;
        view.WebMessageReceived -= OnMessage;
        view.NavigationCompleted -= OnNavigationCompleted;
        view.NavigationStarted -= OnNavigationStarted;
        view.AttachedToVisualTree -= OnAttached;
        view.DetachedFromVisualTree -= OnDetached;
        if (change.OldValue is SimulationSession previous) previous.ExecuteScript = null;
        if (change.NewValue is SimulationSession session)
        {
            view.AdapterCreated += OnAdapterCreated;
            view.WebMessageReceived += OnMessage;
            view.NavigationCompleted += OnNavigationCompleted;
            view.NavigationStarted += OnNavigationStarted;
            view.AttachedToVisualTree += OnAttached;
            view.DetachedFromVisualTree += OnDetached;
            view.SetValue(DocumentReadyProperty, false);
            if (view.AdapterInfo is not null) InitializeView(view);
        }
    });
    private static void OnAdapterCreated(object? sender, WebViewAdapterEventArgs e)
    {
        if (sender is NativeWebView view) InitializeView(view);
    }
    private static async void InitializeView(NativeWebView view)
    {
        if (GetSession(view) is not { } session) return;
        try { await WebViewThemeSupport.InstallAsync(view).WaitAsync(System.TimeSpan.FromSeconds(10)); }
        catch (System.Exception error) { session.AddLog("主题同步初始化失败：" + error.Message); }
        view.Source = session.Address;
    }
    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is NativeWebView view && view.GetValue(DocumentReadyProperty) && GetSession(view) is { } session)
        {
            session.ExecuteScript = view.InvokeScript;
            Dispatcher.UIThread.Post(async () => await session.SetThemeAsync(session.IsDarkTheme));
        }
    }
    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is NativeWebView view && GetSession(view) is { } session) session.ExecuteScript = null;
    }
    private static void OnMessage(object? sender, WebMessageReceivedEventArgs e)
    {
        if (sender is NativeWebView view && GetSession(view) is { } session && e.Body is { } body)
            Dispatcher.UIThread.Post(() => session.Receive(body));
    }
    private static async void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (sender is NativeWebView view && GetSession(view) is { } session)
        {
            view.SetValue(DocumentReadyProperty, true);
            session.ExecuteScript = view.InvokeScript;
            await session.SetThemeAsync(session.IsDarkTheme);
        }
    }
    private static void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (sender is NativeWebView view && GetSession(view) is { } session)
        {
            view.SetValue(DocumentReadyProperty, false);
            session.ExecuteScript = null;
        }
    }
}
