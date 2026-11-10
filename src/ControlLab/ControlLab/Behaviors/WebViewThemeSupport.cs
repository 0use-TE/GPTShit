using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;

namespace ControlLab.Behaviors;

internal static class WebViewThemeSupport
{
    public static Task InstallAsync(NativeWebView view)
    {
        if (!OperatingSystem.IsWindows() || view.TryGetPlatformHandle() is not IWindowsWebView2PlatformHandle handle)
            return Task.CompletedTask;
        return InstallWindows(handle, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Web", "wokwi-theme.js")));
    }

    private static unsafe Task InstallWindows(IWindowsWebView2PlatformHandle handle, string script)
    {
        // Avalonia exposes an owned ICoreWebView2 COM reference. Its stable Win32 ABI
        // places AddScriptToExecuteOnDocumentCreated at slot 27 (after IUnknown).
        // Registering in all documents allows the hosted Wokwi iframe to handle theme
        // messages without reloading its running MCU. No reflection or dynamic COM.
        var webView = handle.CoreWebView2;
        if (webView == 0) return Task.CompletedTask;
        var callback = new ScriptInstalledCallback();
        var callbackPointer = ComInterfaceMarshaller<IScriptInstalledCallback>.ConvertToUnmanaged(callback);
        try
        {
            var table = *(nint**)webView;
            var addScript = (delegate* unmanaged[Stdcall]<nint, char*, void*, int>)table[27];
            fixed (char* text = script) Marshal.ThrowExceptionForHR(addScript(webView, text, callbackPointer));
            return callback.Completion.Task;
        }
        finally
        {
            ComInterfaceMarshaller<IScriptInstalledCallback>.Free(callbackPointer);
            Marshal.Release(webView);
        }
    }
}

[GeneratedComInterface(Options = ComInterfaceOptions.ManagedObjectWrapper)]
[Guid("B99369F3-9B11-47B5-BC6F-8E7895FCEA17")]
internal partial interface IScriptInstalledCallback
{
    void Invoke(int errorCode, [MarshalAs(UnmanagedType.LPWStr)] string id);
}

[GeneratedComClass]
internal partial class ScriptInstalledCallback : IScriptInstalledCallback
{
    public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Invoke(int errorCode, string id)
    {
        if (errorCode < 0) Completion.TrySetException(Marshal.GetExceptionForHR(errorCode) ?? new InvalidOperationException("WebView theme initialization failed"));
        else Completion.TrySetResult();
    }
}
