using System.Net.WebSockets;
using System.Text.Json;
using HeadArena;

if (args.Contains("--self-test")) { GameTests.Run(); return; }
Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5187");
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();
app.Map("/play", async context => {
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    var token = cancellation.Token;
    var game = new Game();
    var gate = new object();
    var reader = Task.Run(async () => {
        var buffer = new byte[4096];
        try {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested) {
                var result = await socket.ReceiveAsync(buffer.AsMemory(), token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (!result.EndOfMessage || result.MessageType != WebSocketMessageType.Text) break;
                try {
                    var input = JsonSerializer.Deserialize<Input>(buffer.AsSpan(0, result.Count), JsonOptions.Value);
                    if (input is not null) lock (gate) game.Handle(input);
                } catch (JsonException) { }
            }
        } catch (OperationCanceledException) { } catch (WebSocketException) { }
        finally { cancellation.Cancel(); }
    }, token);
    try {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / 60));
        while (await timer.WaitForNextTickAsync(token)) {
            byte[] frame;
            lock (gate) { game.Step(1.0 / 60); frame = JsonSerializer.SerializeToUtf8Bytes(game.Snapshot(), JsonOptions.Value); }
            await socket.SendAsync(frame.AsMemory(), WebSocketMessageType.Text, true, token);
        }
    } catch (OperationCanceledException) { } catch (WebSocketException) { }
    finally { cancellation.Cancel(); await reader; }
});
await app.RunAsync();

static class JsonOptions {
    public static readonly JsonSerializerOptions Value = new(JsonSerializerDefaults.Web);
}
