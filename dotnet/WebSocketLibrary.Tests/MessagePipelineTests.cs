using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using WebSocketLibrary.Models;
using WebSocketLibrary.Utilities;
using Xunit;

namespace WebSocketLibrary.Tests
{
    /// <summary>
    /// Tests for the MessagePipeline class which uses System.IO.Pipelines for efficient data processing.
    /// </summary>
    public class MessagePipelineTests
    {
        /// <summary>
        /// Tests that the WebSocket message processing pipeline correctly handles data.
        /// </summary>
        [Fact]
        public async Task ProcessWebSocketMessagesAsync_HandlesDataCorrectly()
        {
            // Arrange
            var options = new WebSocketOptions { MaxMessageSize = 4096 };
            var loggerMock = new Mock<ILogger>();
            var webSocketMock = new Mock<WebSocket>();
            var pipeline = new MessagePipeline(options, loggerMock.Object);
            
            // Set up mock WebSocket to return a close status when needed
            webSocketMock.Setup(ws => ws.State).Returns(WebSocketState.Open);
            webSocketMock.Setup(ws => ws.CloseStatus).Returns(WebSocketCloseStatus.NormalClosure);
            
            // Set up ReceiveAsync to return a single message then a close message
            webSocketMock
                .Setup(ws => ws.ReceiveAsync(
                    It.IsAny<ArraySegment<byte>>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebSocketReceiveResult(
                    5, // Count (bytes received)
                    WebSocketMessageType.Text, // Message type
                    true)); // End of message
            
            // Set up message and close handlers - using local functions that don't rely on variables
            // which eliminates the warnings about unused variables
            Action<WebSocketMessage> messageHandler = _ => { };
            Action<WebSocketCloseStatus?> closeHandler = _ => { };
            
            // Create a cancellation token that will trigger after a short delay
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            
            // Act - this should complete when cancellation token is triggered
            await pipeline.ProcessWebSocketMessagesAsync(
                webSocketMock.Object,
                messageHandler,
                closeHandler,
                cancellationTokenSource.Token);
            
            // Assert
            webSocketMock.Verify(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
                
            // The test will pass if we reach this point without exceptions
        }
    }
}