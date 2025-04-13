using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MyWebSocketServer;

public static partial class WebSocketHandler {
    private static readonly ConcurrentDictionary<string, ClientSession> SessionsByIdMap = [];
    private static readonly TimeSpan ColorMessageInterval = TimeSpan.FromSeconds(10);
    private static readonly Random Rand = new();

    public static async Task HandleAsync(HttpContext _, WebSocket socket) {
        (string sessionId, bool success) = await CreateSession(socket);
        if (!success)
            return;
        Log(sessionId, $"Connected");

        using var cts = new CancellationTokenSource();
        var colorMessageTask = SendColorMessages(socket, sessionId, cts.Token);

        try {
            await ReceiveMessages(socket, sessionId);
        } catch (WebSocketException ex) {
            Log(sessionId, $"WebSocket error - {ex.Message}");
        } catch (Exception ex) {
            Log(sessionId, $"Error: {ex.Message}");
        }
        finally {
            await CleanupSession(socket, sessionId, cts);
        }

        await colorMessageTask;
    }

    private static async Task<(string sessionId, bool success)> CreateSession(WebSocket socket) {
        string sessionId = Guid.NewGuid().ToString();
        var clientSession = new ClientSession(sessionId, socket);
        SessionsByIdMap[sessionId] = clientSession;
        Log(sessionId, $"Created");

        // Send initial connection message
        var responseMessage = JsonSerializer.Serialize(clientSession);
        var responseBuffer = Encoding.UTF8.GetBytes(responseMessage);
        try {
            await socket.SendAsync(new ArraySegment<byte>(responseBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
            Log(sessionId, $"Sent initial connection message - {responseMessage}");
            return (sessionId, true);
        } catch (WebSocketException ex) {
            Log(sessionId, $"WebSocket error while sending initial connection message - {ex.Message}");
        } catch (Exception ex) {
            Log(sessionId, $"Error while sending initial connection message - {ex.Message}");
        }

        // Remove the session from the map if sending the initial message fails
        SessionsByIdMap.TryRemove(sessionId, out _);
        return (sessionId, false);
    }

    private static async Task ReceiveMessages(WebSocket socket, string sessionId) {
        var buffer = new byte[1024 * 4];
        WebSocketReceiveResult result;

        while (true) {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close) {
                Log(sessionId, $"Client initiated close");
                break;
            }

            var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Log(sessionId, $"Received message - {messageText}");
            await ProcessMessage(socket, sessionId, messageText);
        }
    }

    private static async Task ProcessMessage(WebSocket socket, string sessionId, string messageText) {
        var clientSession = SessionsByIdMap[sessionId];

        if (messageText == "A") {
            clientSession.CountA++;
            Log(sessionId, $"Received A, total {clientSession.CountA}");
        } else if (messageText == "B") {
            clientSession.CountB++;
            Log(sessionId, $"Received B, total {clientSession.CountB}");
        } else {
            Log(sessionId, $"Unrecognized message: {messageText}");
        }

        var responseMessage = JsonSerializer.Serialize(clientSession);
        var responseBuffer = Encoding.UTF8.GetBytes(responseMessage);
        await socket.SendAsync(new ArraySegment<byte>(responseBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
        Log(sessionId, $"Sent response - {responseMessage}");
    }

    private static async Task CleanupSession(WebSocket socket, string sessionId, CancellationTokenSource cts) {
        SessionsByIdMap.TryRemove(sessionId, out var _);
        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived) {
            try {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                Log(sessionId, $"Closed WebSocket");
            } catch (WebSocketException ex) {
                Log(sessionId, $"Error while closing WebSocket - {ex.Message}");
            }
        }
        socket.Dispose();
        Log(sessionId, $"Disconnected");
        cts.Cancel();
    }

    private static async Task SendColorMessages(WebSocket socket, string sessionId, CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
                await Task.Delay(ColorMessageInterval, ct);
                if (ct.IsCancellationRequested)
                    break;

                var color = new {
                    R = Rand.Next(128, 256),
                    G = Rand.Next(128, 256),
                    B = Rand.Next(128, 256)
                };
                var colorMessage = JsonSerializer.Serialize(color);
                var colorBuffer = Encoding.UTF8.GetBytes(colorMessage);
                await socket.SendAsync(new ArraySegment<byte>(colorBuffer), WebSocketMessageType.Text, true, ct);
                Log(sessionId, $"Sent color message - {colorMessage}");
            } catch (OperationCanceledException) {
                // Task was cancelled, we're done
                break;
            } catch (WebSocketException ex) {
                Log(sessionId, $"WebSocket error sending color message - {ex.Message}");
                break;
            } catch (Exception ex) {
                Log(sessionId, $"Error sending color message - {ex.Message}");
                break;
            }
        }
    }

    private static void Log(string sessionId, string message) =>
        Console.WriteLine($"[{DateTime.UtcNow:u}] Session {sessionId}: {message}");
}
