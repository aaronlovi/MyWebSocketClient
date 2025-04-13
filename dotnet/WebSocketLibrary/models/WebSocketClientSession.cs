using System;
using System.Net.WebSockets;
using System.Text.Json.Serialization;

namespace WebSocketLibrary.Models
{
    /// <summary>
    /// Represents a connected WebSocket client session.
    /// Maintains client state and connection information.
    /// </summary>
    public class WebSocketClientSession
    {
        /// <summary>
        /// Creates a new WebSocketClientSession with the specified session ID and WebSocket.
        /// </summary>
        /// <param name="sessionId">The unique ID for this client session</param>
        /// <param name="webSocket">The WebSocket connection for this client</param>
        public WebSocketClientSession(string sessionId, WebSocket webSocket)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            WebSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            ConnectedAt = DateTime.UtcNow;
            LastActivityAt = DateTime.UtcNow;
        }

        /// <summary>
        /// The unique identifier for this client session.
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// The WebSocket connection for this client.
        /// Not included in serialization.
        /// </summary>
        [JsonIgnore]
        public WebSocket WebSocket { get; }

        /// <summary>
        /// The time when the client connected, in UTC.
        /// </summary>
        public DateTime ConnectedAt { get; }

        /// <summary>
        /// The time of the last activity for this client, in UTC.
        /// </summary>
        public DateTime LastActivityAt { get; private set; }

        /// <summary>
        /// Any custom user data associated with this session.
        /// May be null if no user data has been set.
        /// </summary>
        public object? UserData { get; set; }

        /// <summary>
        /// Updates the last activity timestamp to the current time.
        /// </summary>
        public void UpdateActivity()
        {
            LastActivityAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Determines if the session has been idle for longer than the specified timeout.
        /// </summary>
        /// <param name="idleTimeout">The idle timeout period</param>
        /// <returns>True if the session is idle, false otherwise</returns>
        public bool IsIdle(TimeSpan idleTimeout)
        {
            return DateTime.UtcNow - LastActivityAt > idleTimeout;
        }
    }
}