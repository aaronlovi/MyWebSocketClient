using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketLibrary.services
{
    /// <summary>
    /// Provides functionality for managing WebSocket heartbeat operations.
    /// </summary>
    public class HeartbeatService
    {
        /// <summary>
        /// Gets or sets the interval between ping frames.
        /// </summary>
        public TimeSpan PingInterval { get; set; }

        /// <summary>
        /// Gets or sets the timeout threshold for detecting connection issues.
        /// </summary>
        public TimeSpan TimeoutThreshold { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HeartbeatService"/> class.
        /// </summary>
        /// <param name="pingInterval">The interval between ping frames.</param>
        /// <param name="timeoutThreshold">The timeout threshold for detecting connection issues.</param>
        public HeartbeatService(TimeSpan pingInterval, TimeSpan timeoutThreshold)
        {
            PingInterval = pingInterval;
            TimeoutThreshold = timeoutThreshold;
        }

        /// <summary>
        /// Sends a WebSocket ping frame.
        /// </summary>
        /// <param name="webSocket">The WebSocket instance to send the ping frame to.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task SendPingAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                byte[] buffer = new byte[0]; // Empty payload for ping frame
                await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, cancellationToken);
            }
        }

        /// <summary>
        /// Starts the heartbeat service asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket instance to send ping frames to.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task StartAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await SendPingAsync(webSocket, cancellationToken);
                await Task.Delay(PingInterval, cancellationToken);
            }
        }
    }
}