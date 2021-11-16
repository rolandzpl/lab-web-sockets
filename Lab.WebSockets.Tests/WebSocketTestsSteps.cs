using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TechTalk.SpecFlow;

[Binding]
public class WebSocketTestsSteps
{
    private TestServer server;
    private WebSocket socket;
    private List<ReceivedPacket> receivedPackets = new List<ReceivedPacket>();

    [Given("Started server with endpoint \"(.+)\"")]
    public void StartedServerWithEndpoint(string endpoint)
    {
        server = new TestServer(new WebHostBuilder()
            .UseKestrel((ctx, opt) =>
            {
                var url = endpoint.Split(':');
                opt.Listen(IPAddress.Parse(url[0]), int.Parse(url[1]));
            })
            .ConfigureServices(_ =>
            {
                _.AddSingleton<WebSocketHandler>(_ =>
                    new TestWebSocketHandler(
                        (soc, buf, res) =>
                        {
                            receivedPackets.Add(new ReceivedPacket
                            {
                                Socket = soc,
                                Buffer = buf,
                                Result = res
                            });
                        },
                        (soc, res) => { }));
            })
            .Configure(_ =>
            {
                _.UseWebSockets();
                _.Map("/ws", app => app.UseMiddleware<WebSocketMiddleware>());
            }));
    }

    [When("Client connects to \"(.+)\"")]
    public async Task ClientConnectsTo(string url)
    {
        var client = server.CreateWebSocketClient();
        socket = await client.ConnectAsync(new Uri(url), CancellationToken.None);
    }

    [When("Sends json ({.+})")]
    public async Task SendsJson(string json)
    {
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    [Then("Server received (\\d+) packets")]
    public void ReceivedPacketCount(int count)
    {
        Assert.That(receivedPackets.Count(), Is.EqualTo(count));
    }

    [Then("Server received json ({.+})")]
    public void ServerReceivedJson(string json)
    {
        Assert.That(receivedPackets
            .Any(_ => Encoding.UTF8
                .GetString(_.Buffer.AsSpan(Range.EndAt(_.Result.Count)))
                .Equals(json)));
    }

    [Then("Servers keeps connection")]
    public void ServerKeepsConnection()
    {
        Assert.That(socket.State, Is.EqualTo(WebSocketState.Open));
    }
}

public class ReceivedPacket
{
    public byte[] Buffer;
    public WebSocketReceiveResult Result;
    public WebSocket Socket;
}

public class TestWebSocketHandler : WebSocketHandler
{
    private readonly Action<WebSocket, byte[], WebSocketReceiveResult> onBytesReceived;
    private readonly Action<WebSocket, WebSocketReceiveResult> onClosed;

    public TestWebSocketHandler(
        Action<WebSocket, byte[], WebSocketReceiveResult> onBytesReceived,
        Action<WebSocket, WebSocketReceiveResult> onClosed)
    {
        this.onBytesReceived = onBytesReceived;
        this.onClosed = onClosed;
    }

    public override void OnClosed(WebSocket socket, WebSocketReceiveResult result)
    {
        onClosed(socket, result);
    }

    public override void OnReceived(WebSocket socket, byte[] buffer, WebSocketReceiveResult result)
    {
        onBytesReceived(socket, buffer, result);
    }
}

public abstract class WebSocketHandler
{
    public abstract void OnClosed(WebSocket socket, WebSocketReceiveResult result);

    public abstract void OnReceived(WebSocket socket, byte[] buffer, WebSocketReceiveResult result);
}

class WebSocketMiddleware
{
    private readonly RequestDelegate next;
    private readonly WebSocketHandler webSocketHandler;
    private readonly ILogger<WebSocketMiddleware> logger;

    public WebSocketMiddleware(RequestDelegate next, WebSocketHandler webSocketHandler, ILogger<WebSocketMiddleware> logger)
    {
        this.next = next;
        this.webSocketHandler = webSocketHandler;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await next(context);
            return;
        }
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        byte[] buffer = new byte[4096];
        while (true)
        {
            if (socket.State != WebSocketState.Open)
            {
                return;
            }
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                webSocketHandler.OnReceived(socket, buffer, result);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                webSocketHandler.OnClosed(socket, result);
                break;
            }
        }
    }
}