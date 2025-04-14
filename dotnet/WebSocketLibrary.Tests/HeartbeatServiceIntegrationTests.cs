using System;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WebSocketLibrary.Contracts;
using WebSocketLibrary.Models;
using WebSocketLibrary.services;
using WebSocketLibrary.Services;
using Xunit;

namespace WebSocketLibrary.Tests
{
    /// <summary>
    /// Integration tests for the HeartbeatService and WebSocketHandler.
    /// </summary>
    public class HeartbeatServiceIntegrationTests
    {
        private readonly Mock<WebSocket> _mockWebSocket;
        private readonly Mock<HttpContext> _mockHttpContext;
        private readonly Mock<ILogger<WebSocketHandler>> _mockLogger;
        private readonly Mock<WebSocketLibrary.services.ITimer> _mockTimer;
        private readonly HeartbeatServiceOptions _heartbeatOptions;
        private readonly HeartbeatService _heartbeatService;
        private readonly WebSocketOptions _webSocketOptions;
        private readonly Mock<IOptions<WebSocketOptions>> _mockWebSocketOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeartbeatServiceIntegrationTests"/> class.
        /// </summary>
        public HeartbeatServiceIntegrationTests()
        {
            _mockWebSocket = new Mock<WebSocket>();
            _mockHttpContext = new Mock<HttpContext>();
            _mockLogger = new Mock<ILogger<WebSocketHandler>>();
            _mockTimer = new Mock<WebSocketLibrary.services.ITimer>();

            _heartbeatOptions = new HeartbeatServiceOptions
            {
                PingInterval = TimeSpan.FromMilliseconds(100),
                TimeoutThreshold = TimeSpan.FromSeconds(5)
            };

            _heartbeatService = new HeartbeatService(_heartbeatOptions, _mockTimer.Object);

            _webSocketOptions = new WebSocketOptions();
            _mockWebSocketOptions = new Mock<IOptions<WebSocketOptions>>();
            _mockWebSocketOptions.Setup(o => o.Value).Returns(_webSocketOptions);
        }

        /// <summary>
        /// Tests that the HeartbeatService sends ping frames when integrated with WebSocketHandler.
        /// </summary>
        [Fact]
        public async Task WebSocketHandler_Integration_InitiatesHeartbeat()
        {
            // Arrange
            var webSocketHandler = new WebSocketHandler(
                _mockWebSocketOptions.Object,
                _mockLogger.Object,
                _heartbeatService);

            _mockWebSocket.Setup(ws => ws.State).Returns(WebSocketState.Open);
            _mockWebSocket.Setup(ws => ws.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

            var cancellationTokenSource = new CancellationTokenSource();

            // Act - Start handling connection
            var connectionTask = webSocketHandler.HandleConnectionAsync(
                _mockHttpContext.Object,
                _mockWebSocket.Object,
                cancellationTokenSource.Token);

            // Wait a bit for the HandleConnectionAsync to start the heartbeat service
            await Task.Delay(50);

            // Create ElapsedEventArgs using reflection since it doesn't have a public constructor
            var elapsedEventArgs = (ElapsedEventArgs)Activator.CreateInstance(typeof(ElapsedEventArgs), 
                BindingFlags.Instance | BindingFlags.NonPublic, 
                null, 
                new object[] { DateTime.Now }, 
                null);
            
            // Simulate timer elapsed event
            _mockTimer.Raise(t => t.Elapsed += null, elapsedEventArgs);

            // Cancel to allow HandleConnectionAsync to complete
            cancellationTokenSource.Cancel();
            try
            {
                await connectionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected, ignoring
            }

            // Assert
            // Verify the WebSocket's SendAsync method was called with an empty payload
            _mockWebSocket.Verify(ws => ws.SendAsync(
                It.Is<ArraySegment<byte>>(b => b.Count == 0),  // Empty ping frame
                WebSocketMessageType.Binary,
                true,
                It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        /// <summary>
        /// Tests that the HeartbeatService properly stops when the WebSocketHandler connection ends.
        /// </summary>
        [Fact]
        public async Task WebSocketHandler_Integration_StopsHeartbeatOnDisconnect()
        {
            // Arrange
            var webSocketHandler = new WebSocketHandler(
                _mockWebSocketOptions.Object,
                _mockLogger.Object,
                _heartbeatService);

            _mockWebSocket.Setup(ws => ws.State).Returns(WebSocketState.Open);

            // Setup WebSocket to disconnect after one receive
            _mockWebSocket.Setup(ws => ws.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

            var cancellationTokenSource = new CancellationTokenSource();

            // Act - Start handling connection
            var connectionTask = webSocketHandler.HandleConnectionAsync(
                _mockHttpContext.Object, 
                _mockWebSocket.Object,
                cancellationTokenSource.Token);

            // Wait a bit for handling to start
            await Task.Delay(50);
            
            // Cancel the token to trigger the Stop() method on the timer
            cancellationTokenSource.Cancel();
            
            try 
            {
                await connectionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected, ignoring
            }

            // Assert - Verify timer was stopped
            _mockTimer.Verify(t => t.Stop(), Times.AtLeastOnce);
        }

        /// <summary>
        /// Tests that unresponsive clients are properly detected and disconnected.
        /// </summary>
        [Fact]
        public async Task HeartbeatService_DetectsAndDisconnectsUnresponsiveClients()
        {
            // Arrange
            _mockWebSocket.Setup(ws => ws.State).Returns(WebSocketState.Open);
            
            // Create an unresponsive client session (last pong older than timeout)
            var clientSession = new WebSocketClientSession(
                "test-session", 
                _mockWebSocket.Object,
                DateTime.UtcNow - TimeSpan.FromSeconds(10));

            // Act
            await _heartbeatService.DisconnectUnresponsiveClientAsync(clientSession);

            // Assert
            _mockWebSocket.Verify(ws => ws.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client unresponsive",
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }
    }
}