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

    public LocalWebHost(StateStore store, int port)
    {
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
            await StreamSnapshotsAsync(socket, store, context.RequestAborted);
        });
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => _app.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) => _app.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static async Task StreamSnapshotsAsync(WebSocket socket, StateStore store, CancellationToken cancellationToken)
    {
        long sequence = 0;
        var lastEventId = 0;
        var counter = 0;
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var snapshot = store.Current;
                await SendAsync(socket, new WireMessage<LivePayload>
                {
                    Type = MessageTypes.Live,
                    Seq = ++sequence,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Payload = SnapshotBuilder.BuildLive(snapshot)
                }, cancellationToken);

                foreach (var evt in SnapshotBuilder.BuildEvents(snapshot).Where(item => item.Id > lastEventId))
                {
                    lastEventId = Math.Max(lastEventId, evt.Id);
                    await SendAsync(socket, new WireMessage<EventRecord>
                    {
                        Type = MessageTypes.Event,
                        Seq = ++sequence,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Payload = evt
                    }, cancellationToken);
                }

                if (counter++ % 5 == 0)
                {
                    await SendAsync(socket, new WireMessage<FieldPayload>
                    {
                        Type = MessageTypes.Field,
                        Seq = ++sequence,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Payload = SnapshotBuilder.BuildField(snapshot)
                    }, cancellationToken);
                }

                await Task.Delay(Constants.LiveSnapshotIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (WebSocketException) { }
    }

    private static async Task SendAsync<T>(WebSocket socket, WireMessage<T> message, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonDefaults.Options);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }
}
