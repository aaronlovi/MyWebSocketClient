using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebSocketLibrary.Contracts;
using WebSocketLibrary.Models;

namespace WebSocketLibrary.Services
{
    /// <summary>
    /// Handles WebSocket connections, message sending/receiving, and connection management.
    /// </summary>
    public class WebSocketHandler : IWebSocketHandler
    {
        private readonly WebSocketOptions _options;
        private readonly ILogger<WebSocketHandler> _logger;
        private readonly ConcurrentDictionary<string, WebSocketClientSession> _sessions = new();
        private readonly Timer _idleClientTimer;

        /// <summary>
        /// Event raised when a client connects to the WebSocket server.
        /// </summary>
        public event EventHandler<WebSocketClientSession> ClientConnected;
        
        /// <summary>
        /// Event raised when a client disconnects from the WebSocket server.
        /// </summary>
        public event EventHandler<WebSocketClientSession> ClientDisconnected;
        
        /// <summary>
        /// Event raised when a message is received from a client.
        /// </summary>
        public event EventHandler<(WebSocketClientSession session, WebSocketMessage message)> MessageReceived;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketHandler"/> class.
        /// </summary>
        /// <param name="options">The WebSocket configuration options</param>
        /// <param name="logger">The logger for WebSocketHandler</param>
        public WebSocketHandler(IOptions<WebSocketOptions> options, ILogger<WebSocketHandler> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Start a timer to periodically check for and disconnect idle clients
            _idleClientTimer = new Timer(
                async _ => await DisconnectIdleClientsAsync(CancellationToken.None), 
                null, 
                TimeSpan.FromSeconds(30), 
                TimeSpan.FromSeconds(30));
        }

        /// <inheritdoc/>
        public async Task HandleConnectionAsync(HttpContext context, WebSocket webSocket, CancellationToken cancellationToken)
        {
            if (webSocket == null)
            {
                throw new ArgumentNullException(nameof(webSocket));
            }

            // Create a session for the client
            var sessionId = Guid.NewGuid().ToString();
            var session = new WebSocketClientSession(sessionId, webSocket);
            
            if (!_sessions.TryAdd(sessionId, session))
            {
                _logger.LogWarning("Failed to add session {SessionId} to sessions dictionary", sessionId);
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, 
                    "Failed to create session", cancellationToken);
                return;
            }
            
            _logger.LogInformation("Client {SessionId} connected", sessionId);
            
            // Raise the client connected event
            OnClientConnected(session);
            
            try
            {
                // Process messages from the client
                await ReceiveMessagesAsync(session, cancellationToken);
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket error for client {SessionId}: {Message}", 
                    sessionId, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling WebSocket connection for client {SessionId}: {Message}", 
                    sessionId, ex.Message);
            }
            finally
            {
                // Clean up the session
                await RemoveSessionAsync(sessionId, WebSocketCloseStatus.NormalClosure, 
                    "Connection closed", cancellationToken);
            }
        }

        /// <inheritdoc/>
        public async Task SendMessageAsync(string sessionId, WebSocketMessage message, CancellationToken cancellationToken)
        {
            if (sessionId == null) throw new ArgumentNullException(nameof(sessionId));
            if (message == null) throw new ArgumentNullException(nameof(message));
            
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                await SendMessageInternalAsync(session, message, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Attempted to send message to unknown session {SessionId}", sessionId);
            }
        }

        /// <inheritdoc/>
        public async Task BroadcastMessageAsync(WebSocketMessage message, CancellationToken cancellationToken)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            
            var tasks = new List<Task>();
            
            foreach (var session in _sessions.Values)
            {
                if (session.WebSocket.State == WebSocketState.Open)
                {
                    tasks.Add(SendMessageInternalAsync(session, message, cancellationToken));
                }
            }
            
            await Task.WhenAll(tasks);
        }

        /// <inheritdoc/>
        public async Task DisconnectClientAsync(string sessionId, WebSocketCloseStatus status, string statusDescription, CancellationToken cancellationToken)
        {
            if (sessionId == null) throw new ArgumentNullException(nameof(sessionId));
            
            await RemoveSessionAsync(sessionId, status, statusDescription, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DisconnectIdleClientsAsync(CancellationToken cancellationToken)
        {
            var idleSessionIds = _sessions.Values
                .Where(s => s.IsIdle(_options.IdleTimeout))
                .Select(s => s.SessionId)
                .ToList();

            foreach (var sessionId in idleSessionIds)
            {
                _logger.LogInformation("Disconnecting idle client {SessionId}", sessionId);
                await RemoveSessionAsync(sessionId, WebSocketCloseStatus.NormalClosure, 
                    "Client idle timeout", cancellationToken);
            }
        }
        
        /// <summary>
        /// Processes incoming messages from a client.
        /// </summary>
        /// <param name="session">The client session</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task ReceiveMessagesAsync(WebSocketClientSession session, CancellationToken cancellationToken)
        {
            var buffer = new byte[_options.MaxMessageSize];
            var sessionId = session.SessionId;

            while (session.WebSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                
                using var ms = new System.IO.MemoryStream();
                
                // Read the message
                do
                {
                    result = await session.WebSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Client {SessionId} initiated close", sessionId);
                        await RemoveSessionAsync(sessionId, WebSocketCloseStatus.NormalClosure, 
                            "Client requested close", cancellationToken);
                        return;
                    }

                    // Write received data to the MemoryStream
                    await ms.WriteAsync(buffer, 0, result.Count, cancellationToken);
                }
                while (!result.EndOfMessage);
                
                // Reset position to read from the beginning
                ms.Position = 0;
                
                // Convert the message to a byte array
                var messageData = ms.ToArray();
                
                // Create a message object
                var message = new WebSocketMessage(result.MessageType, messageData);
                
                // Update activity timestamp
                session.UpdateActivity();
                
                // Process the message
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var textContent = message.GetTextContent();
                    _logger.LogDebug("Received text message from {SessionId}: {Message}", 
                        sessionId, textContent);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    _logger.LogDebug("Received binary message from {SessionId}: {Length} bytes", 
                        sessionId, messageData.Length);
                }
                
                // Raise the message received event
                OnMessageReceived(session, message);
                
                // Handle ping with pong response
                if (result.MessageType == WebSocketMessageType.Binary && messageData.Length == 1 && messageData[0] == 0x09)
                {
                    _logger.LogDebug("Received ping from {SessionId}, sending pong", sessionId);
                    var pongData = new byte[] { 0x0A }; // pong frame
                    await session.WebSocket.SendAsync(new ArraySegment<byte>(pongData), 
                        WebSocketMessageType.Binary, true, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Sends a WebSocket message to a client.
        /// </summary>
        /// <param name="session">The client session</param>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task SendMessageInternalAsync(WebSocketClientSession session, WebSocketMessage message, 
            CancellationToken cancellationToken)
        {
            try
            {
                if (session.WebSocket.State == WebSocketState.Open)
                {
                    await session.WebSocket.SendAsync(
                        new ArraySegment<byte>(message.Data), 
                        message.MessageType,
                        message.EndOfMessage,
                        cancellationToken);
                    
                    session.UpdateActivity();
                    
                    if (message.MessageType == WebSocketMessageType.Text)
                    {
                        _logger.LogDebug("Sent text message to {SessionId}: {Length} bytes", 
                            session.SessionId, message.Data.Length);
                    }
                    else if (message.MessageType == WebSocketMessageType.Binary)
                    {
                        _logger.LogDebug("Sent binary message to {SessionId}: {Length} bytes", 
                            session.SessionId, message.Data.Length);
                    }
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket error sending message to {SessionId}: {Message}", 
                    session.SessionId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Removes a client session and closes the WebSocket connection.
        /// </summary>
        /// <param name="sessionId">The session ID to remove</param>
        /// <param name="status">The close status to send to the client</param>
        /// <param name="statusDescription">The close description to send to the client</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task RemoveSessionAsync(string sessionId, WebSocketCloseStatus status, 
            string statusDescription, CancellationToken cancellationToken)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                try
                {
                    if (session.WebSocket.State == WebSocketState.Open)
                    {
                        // Try to close the WebSocket gracefully
                        await session.WebSocket.CloseAsync(
                            status,
                            statusDescription,
                            cancellationToken);
                    }
                }
                catch (WebSocketException ex)
                {
                    _logger.LogWarning(ex, "Error closing WebSocket for {SessionId}: {Message}", 
                        sessionId, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while closing WebSocket for {SessionId}: {Message}", 
                        sessionId, ex.Message);
                }
                finally
                {
                    // Always dispose the WebSocket to free resources
                    session.WebSocket.Dispose();
                    
                    _logger.LogInformation("Client {SessionId} disconnected", sessionId);
                    
                    // Raise the client disconnected event
                    OnClientDisconnected(session);
                }
            }
        }

        /// <summary>
        /// Raises the ClientConnected event.
        /// </summary>
        /// <param name="session">The client session that connected</param>
        protected virtual void OnClientConnected(WebSocketClientSession session)
        {
            ClientConnected?.Invoke(this, session);
        }

        /// <summary>
        /// Raises the ClientDisconnected event.
        /// </summary>
        /// <param name="session">The client session that disconnected</param>
        protected virtual void OnClientDisconnected(WebSocketClientSession session)
        {
            ClientDisconnected?.Invoke(this, session);
        }

        /// <summary>
        /// Raises the MessageReceived event.
        /// </summary>
        /// <param name="session">The client session that sent the message</param>
        /// <param name="message">The received message</param>
        protected virtual void OnMessageReceived(WebSocketClientSession session, WebSocketMessage message)
        {
            MessageReceived?.Invoke(this, (session, message));
        }
    }
}