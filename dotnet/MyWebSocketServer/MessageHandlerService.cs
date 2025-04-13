using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketLibrary.Contracts;
using WebSocketLibrary.Models;

namespace MyWebSocketServer;

/// <summary>
/// Service that handles WebSocket message processing.
/// Subscribes to WebSocketHandler events to handle client connections, disconnections, and messages.
/// </summary>
public class MessageHandlerService
{
    private readonly IWebSocketHandler _webSocketHandler;
    private readonly ILogger<MessageHandlerService> _logger;

    /// <summary>
    /// Initializes a new instance of the MessageHandlerService class.
    /// </summary>
    /// <param name="webSocketHandler">The WebSocket handler</param>
    /// <param name="logger">The logger</param>
    public MessageHandlerService(IWebSocketHandler webSocketHandler, ILogger<MessageHandlerService> logger)
    {
        _webSocketHandler = webSocketHandler ?? throw new ArgumentNullException(nameof(webSocketHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to WebSocket events
        _webSocketHandler.ClientConnected += OnClientConnected;
        _webSocketHandler.ClientDisconnected += OnClientDisconnected;
        _webSocketHandler.MessageReceived += OnMessageReceived;

        _logger.LogInformation("MessageHandlerService initialized and ready");
    }

    /// <summary>
    /// Handler for client connection events.
    /// </summary>
    /// <param name="sender">The event sender</param>
    /// <param name="session">The client session that connected</param>
    private void OnClientConnected(object? sender, WebSocketClientSession session)
    {
        _logger.LogInformation("Client connected: {SessionId}", session.SessionId);

        // Send a welcome message to the client
        SendWelcomeMessage(session);
    }

    /// <summary>
    /// Handler for client disconnection events.
    /// </summary>
    /// <param name="sender">The event sender</param>
    /// <param name="session">The client session that disconnected</param>
    private void OnClientDisconnected(object? sender, WebSocketClientSession session)
    {
        _logger.LogInformation("Client disconnected: {SessionId}", session.SessionId);
    }

    /// <summary>
    /// Handler for message received events.
    /// </summary>
    /// <param name="sender">The event sender</param>
    /// <param name="data">Tuple containing the client session and the received message</param>
    private async void OnMessageReceived(object? sender, (WebSocketClientSession session, WebSocketMessage message) data)
    {
        var (session, message) = data;

        if (message.MessageType == WebSocketMessageType.Text)
        {
            // Handle text message
            string? textContent = message.GetTextContent();
            _logger.LogInformation("Received text message from {SessionId}: {Message}", 
                session.SessionId, textContent);

            // Echo the message back to the sender with a prefix
            if (textContent != null)
            {
                await EchoMessageAsync(session, textContent);
            }
        }
        else if (message.MessageType == WebSocketMessageType.Binary)
        {
            // Handle binary message
            _logger.LogInformation("Received binary message from {SessionId}: {Length} bytes", 
                session.SessionId, message.Data.Length);

            // Echo the binary data back
            await _webSocketHandler.SendMessageAsync(
                session.SessionId, 
                WebSocketMessage.CreateBinaryMessage(message.Data),
                CancellationToken.None);
        }
    }

    /// <summary>
    /// Sends a welcome message to newly connected clients.
    /// </summary>
    /// <param name="session">The client session</param>
    private async void SendWelcomeMessage(WebSocketClientSession session)
    {
        var welcomeMessage = $"Welcome to WebSocket Server! Your session ID is: {session.SessionId}";
        await _webSocketHandler.SendMessageAsync(
            session.SessionId,
            WebSocketMessage.CreateTextMessage(welcomeMessage),
            CancellationToken.None);
    }

    /// <summary>
    /// Echoes a received message back to the sender.
    /// </summary>
    /// <param name="session">The client session</param>
    /// <param name="message">The message to echo</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task EchoMessageAsync(WebSocketClientSession session, string message)
    {
        var response = $"Echo: {message}";
        await _webSocketHandler.SendMessageAsync(
            session.SessionId,
            WebSocketMessage.CreateTextMessage(response),
            CancellationToken.None);
    }
}