using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WebSocketLibrary.Models;
using Timer = System.Timers.Timer;

namespace WebSocketLibrary.services
{
    /// <summary>
    /// Represents a timer interface for dependency injection.
    /// </summary>
    public interface ITimer
    {
        /// <summary>
        /// Occurs when the timer interval elapses.
        /// </summary>
        event ElapsedEventHandler Elapsed;

        /// <summary>
        /// Starts the timer.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the timer.
        /// </summary>
        void Stop();
    }

    /// <summary>
    /// A wrapper for the System.Timers.Timer class that implements the ITimer interface.
    /// </summary>
    public class TimerWrapper : ITimer
    {
        private readonly Timer _timer;

        /// <summary>
        /// Initializes a new instance of the TimerWrapper class.
        /// </summary>
        /// <param name="interval">The interval, in milliseconds, at which to raise the Elapsed event.</param>
        public TimerWrapper(double interval)
        {
            _timer = new Timer(interval);
        }

        /// <summary>
        /// Occurs when the timer interval elapses.
        /// </summary>
        public event ElapsedEventHandler Elapsed
        {
            add => _timer.Elapsed += value;
            remove => _timer.Elapsed -= value;
        }

        /// <summary>
        /// Starts the timer.
        /// </summary>
        public void Start() => _timer.Start();

        /// <summary>
        /// Stops the timer.
        /// </summary>
        public void Stop() => _timer.Stop();
    }

    /// <summary>
    /// Provides functionality for managing WebSocket heartbeat operations.
    /// </summary>
    public class HeartbeatService
    {
        private readonly ITimer _timer;

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
        /// <param name="timer">The timer instance to use for triggering pings.</param>
        public HeartbeatService(TimeSpan pingInterval, TimeSpan timeoutThreshold, ITimer timer)
        {
            PingInterval = pingInterval;
            TimeoutThreshold = timeoutThreshold;
            _timer = timer;
        }

        /// <summary>
        /// Starts the heartbeat service asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket instance to send ping frames to.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task StartAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            _timer.Elapsed += async (sender, args) =>
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    byte[] buffer = new byte[0];
                    await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, cancellationToken);
                }
            };

            _timer.Start();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                _timer.Stop();
            }
        }

        /// <summary>
        /// Checks if the client is unresponsive based on the last pong response.
        /// </summary>
        /// <param name="clientSession">The WebSocket client session to check.</param>
        /// <returns>True if the client is unresponsive, false otherwise.</returns>
        public bool IsClientUnresponsive(WebSocketClientSession clientSession) => DateTime.UtcNow - clientSession.LastPongAt > TimeoutThreshold;

        /// <summary>
        /// Disconnects a client if it is unresponsive.
        /// </summary>
        /// <param name="clientSession">The WebSocket client session to disconnect.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task DisconnectUnresponsiveClientAsync(WebSocketClientSession clientSession)
        {
            if (IsClientUnresponsive(clientSession) && clientSession.WebSocket.State == WebSocketState.Open)
            {
                await clientSession.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client unresponsive", CancellationToken.None);
            }
        }
    }
}