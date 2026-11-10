using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
namespace ControlLab.Services;

public sealed class LocalWebServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "Web");
    public Uri Address { get; }
    public LocalWebServer()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        Address = new Uri($"http://localhost:{port}/");
        _listener.Prefixes.Add(Address.AbsoluteUri);
        _listener.Start();
        _ = ListenAsync();
    }
    private async Task ListenAsync()
    {
        while (_listener.IsListening)
        {
            try { var context = await _listener.GetContextAsync(); _ = ServeAsync(context); }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }
    private async Task ServeAsync(HttpListenerContext context)
    {
        try
        {
            var relative = Uri.UnescapeDataString(context.Request.Url!.AbsolutePath).TrimStart('/');
            if (relative.Length == 0) relative = "index.html";
            var path = Path.GetFullPath(Path.Combine(_root, relative));
            if (context.Request.HttpMethod != "GET" ||
                !path.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(path)) { context.Response.StatusCode = 404; return; }
            context.Response.ContentType = Path.GetExtension(path) switch
            {
                ".html" => "text/html; charset=utf-8", ".js" => "text/javascript; charset=utf-8",
                ".json" => "application/json; charset=utf-8", _ => "text/plain; charset=utf-8"
            };
            context.Response.Headers["Cache-Control"] = "no-store";
            var bytes = await File.ReadAllBytesAsync(path);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        catch (Exception) { /* Reload and shutdown can disconnect the browser. */ }
        finally { context.Response.Close(); }
    }
    public void Dispose() => _listener.Close();
}
