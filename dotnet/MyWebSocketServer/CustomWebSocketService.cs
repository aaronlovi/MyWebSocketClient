using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketLibrary.Contracts;
using WebSocketLibrary.Models;

namespace MyWebSocketServer;

/// <summary>
/// Custom WebSocket service that provides specific functionality for the MyWebSocketServer application.
/// </summary>
public class CustomWebSocketService {
    private readonly IWebSocketHandler _webSocketHandler;
    private readonly ILogger<CustomWebSocketService> _logger;
    private readonly ConcurrentDictionary<string, ClientSession> _sessionsByIdMap = new();
    private readonly TimeSpan _colorMessageInterval = TimeSpan.FromSeconds(10);
    private readonly Random _rand = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _colorMessageTokenSources = new();

    /// <summary>
    /// Initializes a new instance of the CustomWebSocketService class.
    /// </summary>
    /// <param name="webSocketHandler">The WebSocket handler from our library</param>
    /// <param name="logger">The logger</param>
    public CustomWebSocketService(IWebSocketHandler webSocketHandler, ILogger<CustomWebSocketService> logger) {
        _webSocketHandler = webSocketHandler ?? throw new ArgumentNullException(nameof(webSocketHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to WebSocket events from our library
        _webSocketHandler.ClientConnected += OnClientConnected;
        _webSocketHandler.ClientDisconnected += OnClientDisconnected;
        _webSocketHandler.MessageReceived += OnMessageReceived;

        _logger.LogInformation("CustomWebSocketService initialized");
    }

    /// <summary>
    /// Handler for client connection events.
    /// </summary>
    private void OnClientConnected(object? sender, WebSocketClientSession session) {
        _logger.LogInformation("[{Time:u}] Session {SessionId}: Connected", DateTime.UtcNow, session.SessionId);

        // Create a new client session
        var clientSession = new ClientSession(session.SessionId);
        _sessionsByIdMap[session.SessionId] = clientSession;

        // Send initial connection message
        SendInitialConnectionMessage(session.SessionId, clientSession);

        // Start sending color messages
        StartColorMessages(session.SessionId, session.WebSocket);
    }

    /// <summary>
    /// Handler for client disconnection events.
    /// </summary>
    private void OnClientDisconnected(object? sender, WebSocketClientSession session) {
        _logger.LogInformation("[{Time:u}] Session {SessionId}: Disconnected", DateTime.UtcNow, session.SessionId);

        // Remove from our session map
        _sessionsByIdMap.TryRemove(session.SessionId, out _);

        // Stop sending color messages
        if (_colorMessageTokenSources.TryRemove(session.SessionId, out var cts)) {
            cts.Cancel();
            cts.Dispose();
        }
    }

    /// <summary>
    /// Handler for message received events.
    /// </summary>
    private async void OnMessageReceived(object? sender, (WebSocketClientSession session, WebSocketMessage message) data) {
        var (session, message) = data;

        if (message.MessageType == WebSocketMessageType.Text) {
            string? messageText = message.GetTextContent();
            if (messageText == null)
                return;

            _logger.LogInformation("[{Time:u}] Session {SessionId}: Received message - {Message}",
                DateTime.UtcNow, session.SessionId, messageText);

            await ProcessMessage(session.SessionId, messageText);
        }
    }

    /// <summary>
    /// Sends the initial connection message to a client.
    /// </summary>
    private async void SendInitialConnectionMessage(string sessionId, ClientSession clientSession) {
        try {
            var responseMessage = JsonSerializer.Serialize(clientSession);
            await _webSocketHandler.SendMessageAsync(
                sessionId,
                WebSocketMessage.CreateTextMessage(responseMessage),
                CancellationToken.None);

            _logger.LogInformation("[{Time:u}] Session {SessionId}: Sent initial connection message - {Message}",
                DateTime.UtcNow, sessionId, responseMessage);
        } catch (Exception ex) {
            _logger.LogError(ex, "[{Time:u}] Session {SessionId}: Error while sending initial connection message",
                DateTime.UtcNow, sessionId);
        }
    }

    /// <summary>
    /// Processes a message received from a client.
    /// </summary>
    private async Task ProcessMessage(string sessionId, string messageText) {
        if (!_sessionsByIdMap.TryGetValue(sessionId, out var clientSession)) {
            _logger.LogWarning("[{Time:u}] Session {SessionId}: Session not found for processing message",
                DateTime.UtcNow, sessionId);
            return;
        }

        if (messageText == "A") {
            clientSession.CountA++;
            _logger.LogInformation("[{Time:u}] Session {SessionId}: Received A, total {Count}",
                DateTime.UtcNow, sessionId, clientSession.CountA);
        } else if (messageText == "B") {
            clientSession.CountB++;
            _logger.LogInformation("[{Time:u}] Session {SessionId}: Received B, total {Count}",
                DateTime.UtcNow, sessionId, clientSession.CountB);
        } else {
            _logger.LogInformation("[{Time:u}] Session {SessionId}: Unrecognized message: {Message}",
                DateTime.UtcNow, sessionId, messageText);
        }

        var responseMessage = JsonSerializer.Serialize(clientSession);
        await _webSocketHandler.SendMessageAsync(
            sessionId,
            WebSocketMessage.CreateTextMessage(responseMessage),
            CancellationToken.None);

        _logger.LogInformation("[{Time:u}] Session {SessionId}: Sent response - {Message}",
            DateTime.UtcNow, sessionId, responseMessage);
    }

    /// <summary>
    /// Starts sending periodic color messages to a client.
    /// </summary>
    private void StartColorMessages(string sessionId, WebSocket socket) {
        var cts = new CancellationTokenSource();
        _colorMessageTokenSources[sessionId] = cts;

        // Fire and forget task to send color messages
        _ = Task.Run(() => SendColorMessages(socket, sessionId, cts.Token));
    }

    /// <summary>
    /// Sends periodic color messages to a client until cancelled.
    /// </summary>
    private async Task SendColorMessages(WebSocket socket, string sessionId, CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
                await Task.Delay(_colorMessageInterval, ct);
                if (ct.IsCancellationRequested)
                    break;

                var color = new {
                    R = _rand.Next(128, 256),
                    G = _rand.Next(128, 256),
                    B = _rand.Next(128, 256)
                };
                var colorMessage = JsonSerializer.Serialize(color);

                await _webSocketHandler.SendMessageAsync(
                    sessionId,
                    WebSocketMessage.CreateTextMessage(colorMessage),
                    ct);

                _logger.LogInformation("[{Time:u}] Session {SessionId}: Sent color message - {Message}",
                    DateTime.UtcNow, sessionId, colorMessage);
            } catch (OperationCanceledException) {
                // Task was cancelled, we're done
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "[{Time:u}] Session {SessionId}: Error sending color message",
                    DateTime.UtcNow, sessionId);
                break;
            }
        }
    }
}