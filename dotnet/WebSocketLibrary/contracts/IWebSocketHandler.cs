using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebSocketLibrary.Models;

namespace WebSocketLibrary.Contracts
{
    /// <summary>
    /// Interface for WebSocket connection handler.
    /// Provides methods to handle client connections and process messages.
    /// </summary>
    public interface IWebSocketHandler {
        /// <summary>
        /// Handles a new WebSocket connection.
        /// </summary>
        /// <param name="context">The HTTP context for the connection</param>
        /// <param name="webSocket">The WebSocket to be handled</param>
        /// <param name="cancellationToken">Cancellation token for canceling the operation</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task HandleConnectionAsync(HttpContext context, WebSocket webSocket, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a message to a specific client by session ID.
        /// </summary>
        /// <param name="sessionId">The target client's session ID</param>
        /// <param name="message">The message to send</param>
        /// <param name="cancellationToken">Cancellation token for canceling the operation</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SendMessageAsync(string sessionId, WebSocketMessage message, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a message to all connected clients.
        /// </summary>
        /// <param name="message">The message to broadcast</param>
        /// <param name="cancellationToken">Cancellation token for canceling the operation</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task BroadcastMessageAsync(WebSocketMessage message, CancellationToken cancellationToken);

        /// <summary>
        /// Disconnects a client by session ID.
        /// </summary>
        /// <param name="sessionId">The client's session ID</param>
        /// <param name="status">The WebSocket close status</param>
        /// <param name="statusDescription">Description of why the connection is being closed</param>
        /// <param name="cancellationToken">Cancellation token for canceling the operation</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task DisconnectClientAsync(string sessionId, WebSocketCloseStatus status, string statusDescription, CancellationToken cancellationToken);

        /// <summary>
        /// Disconnects idle clients that have been inactive for longer than the configured timeout.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for canceling the operation</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task DisconnectIdleClientsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Occurs when a client connects to the WebSocket server.
        /// </summary>
        event EventHandler<WebSocketClientSession> ClientConnected;

        /// <summary>
        /// Occurs when a client disconnects from the WebSocket server.
        /// </summary>
        event EventHandler<WebSocketClientSession> ClientDisconnected;

        /// <summary>
        /// Occurs when a message is received from a client.
        /// </summary>
        event EventHandler<(WebSocketClientSession session, WebSocketMessage message)> MessageReceived;
    }
}