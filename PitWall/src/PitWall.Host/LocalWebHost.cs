using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using PitWall.Contracts;
using PitWall.Contracts.Messages;
using PitWall.Core.State;

namespace PitWall.Host;

internal sealed class LocalWebHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly LocalCallHub _callHub = new();

    public LocalWebHost(StateStore store, int port, Action<CallMessage>? onCallReceived = null)
    {
        if (onCallReceived is not null)
            _callHub.CallReceived += onCallReceived;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        _app = builder.Build();
        _app.UseWebSockets();

        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (!Directory.Exists(webRoot))
            webRoot = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "src", "PitWall.Web", "dist"));
        if (Directory.Exists(webRoot))
        {
            _app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(webRoot) });
            _app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(webRoot) });
            var index = Path.Combine(webRoot, "index.html");
            _app.MapFallback(async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.SendFileAsync(index);
            });
        }

        _app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var peer = _callHub.Add(socket);
            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            try
            {
                var sending = _callHub.SendSnapshotsAsync(peer, store, connectionCts.Token);
                var receiving = _callHub.ReceiveAsync(peer, connectionCts.Token);
                await Task.WhenAny(sending, receiving);
                connectionCts.Cancel();
                try { await Task.WhenAll(sending, receiving); }
                catch (OperationCanceledException) { }
                catch (WebSocketException) { }
            }
            finally
            {
                _callHub.Remove(peer);
            }
        });
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => _app.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) => _app.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

}
