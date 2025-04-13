using System;
using System.IO;
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
    /// Tests for the WebSocketFrameHandler class, which manages WebSocket frame fragmentation and reassembly.
    /// </summary>
    public class WebSocketFrameHandlerTests
    {
        /// <summary>
        /// Verifies that a large message is fragmented into multiple frames.
        /// </summary>
        [Fact]
        public async Task SendMessageAsync_LargeMessage_FragmentsIntoMultipleFrames()
        {
            // Arrange
            var options = new WebSocketOptions { MaxFrameSize = 100 }; // Small max frame size for testing
            var loggerMock = new Mock<ILogger>();
            var webSocketMock = new Mock<WebSocket>();
            var frameHandler = new WebSocketFrameHandler(options, loggerMock.Object);
            
            // Create a message larger than the max frame size
            byte[] largeData = new byte[250];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }
            
            var largeMessage = WebSocketMessage.CreateBinaryMessage(largeData);
            
            int sendCallCount = 0;
            
            // Set up the mock to count how many times SendAsync is called
            webSocketMock.Setup(ws => ws.SendAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<WebSocketMessageType>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>((data, type, endOfMessage, token) =>
                {
                    sendCallCount++;
                })
                .Returns(Task.CompletedTask);
            
            // Act
            await frameHandler.SendMessageAsync(webSocketMock.Object, largeMessage, CancellationToken.None);
            
            // Assert
            // We expect 3 frames (250 bytes / 100 bytes per frame, ceiling)
            Assert.Equal(3, sendCallCount);
            
            // Verify the right parameters were used
            webSocketMock.Verify(ws => ws.SendAsync(
                It.Is<ArraySegment<byte>>(d => d.Count <= options.MaxFrameSize),
                WebSocketMessageType.Binary,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }
        
        /// <summary>
        /// Verifies that small messages are sent in a single frame.
        /// </summary>
        [Fact]
        public async Task SendMessageAsync_SmallMessage_SendsInSingleFrame()
        {
            // Arrange
            var options = new WebSocketOptions { MaxFrameSize = 100 };
            var loggerMock = new Mock<ILogger>();
            var webSocketMock = new Mock<WebSocket>();
            var frameHandler = new WebSocketFrameHandler(options, loggerMock.Object);
            
            // Create a small message
            byte[] smallData = new byte[50]; // Less than MaxFrameSize
            var smallMessage = WebSocketMessage.CreateBinaryMessage(smallData);
            
            int sendCallCount = 0;
            
            // Set up the mock to count how many times SendAsync is called
            webSocketMock.Setup(ws => ws.SendAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<WebSocketMessageType>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>((data, type, endOfMessage, token) =>
                {
                    sendCallCount++;
                    Assert.True(endOfMessage); // Should be marked as end of message
                })
                .Returns(Task.CompletedTask);
            
            // Act
            await frameHandler.SendMessageAsync(webSocketMock.Object, smallMessage, CancellationToken.None);
            
            // Assert
            Assert.Equal(1, sendCallCount); // Should only send once
        }
        
        /// <summary>
        /// Verifies that message fragments are properly reassembled.
        /// </summary>
        [Fact]
        public void ProcessFragment_MultipleFragments_ReassemblesCorrectly()
        {
            // Arrange
            var options = new WebSocketOptions { MaxFrameSize = 100 };
            var loggerMock = new Mock<ILogger>();
            var frameHandler = new WebSocketFrameHandler(options, loggerMock.Object);
            string sessionId = "test-session";
            
            // Create message fragments
            byte[] fragment1Data = new byte[50];
            byte[] fragment2Data = new byte[50];
            byte[] fragment3Data = new byte[50];
            
            // Fill with distinguishable data
            for (int i = 0; i < fragment1Data.Length; i++) fragment1Data[i] = 1;
            for (int i = 0; i < fragment2Data.Length; i++) fragment2Data[i] = 2;
            for (int i = 0; i < fragment3Data.Length; i++) fragment3Data[i] = 3;
            
            var fragment1 = new WebSocketMessage(WebSocketMessageType.Binary, fragment1Data, false, 0);
            var fragment2 = new WebSocketMessage(WebSocketMessageType.Binary, fragment2Data, false, 1);
            var fragment3 = new WebSocketMessage(WebSocketMessageType.Binary, fragment3Data, true, 2);
            
            // Act & Assert
            
            // Process first fragment - should return null as message is incomplete
            var result1 = frameHandler.ProcessFragment(sessionId, fragment1);
            Assert.Null(result1);
            
            // Process second fragment - should return null as message is still incomplete
            var result2 = frameHandler.ProcessFragment(sessionId, fragment2);
            Assert.Null(result2);
            
            // Process final fragment - should return the complete message
            var completeMessage = frameHandler.ProcessFragment(sessionId, fragment3);
            
            // Assert the complete message is correct
            Assert.NotNull(completeMessage);
            Assert.Equal(150, completeMessage.Size); // 50 + 50 + 50
            
            // Verify content
            for (int i = 0; i < 50; i++) Assert.Equal(1, completeMessage.Data[i]);
            for (int i = 50; i < 100; i++) Assert.Equal(2, completeMessage.Data[i]);
            for (int i = 100; i < 150; i++) Assert.Equal(3, completeMessage.Data[i]);
        }
        
        /// <summary>
        /// Verifies that message fragments of different types are rejected.
        /// </summary>
        [Fact]
        public void ProcessFragment_TypeMismatch_ThrowsException()
        {
            // Arrange
            var options = new WebSocketOptions { MaxFrameSize = 100 };
            var loggerMock = new Mock<ILogger>();
            var frameHandler = new WebSocketFrameHandler(options, loggerMock.Object);
            string sessionId = "test-session";
            
            // Create fragments with different message types
            var fragment1 = new WebSocketMessage(WebSocketMessageType.Binary, new byte[10], false, 0);
            var fragment2 = new WebSocketMessage(WebSocketMessageType.Text, new byte[10], true, 1);
            
            // Act & Assert
            
            // Process first fragment
            var result1 = frameHandler.ProcessFragment(sessionId, fragment1);
            
            // Second fragment should throw an exception due to message type mismatch
            var exception = Assert.Throws<InvalidDataException>(() => 
                frameHandler.ProcessFragment(sessionId, fragment2));
            
            Assert.Contains("Message type mismatch", exception.Message);
        }
        
        /// <summary>
        /// Verifies that clearing a session removes any incomplete fragments.
        /// </summary>
        [Fact]
        public void ClearSession_RemovesIncompleteFragments()
        {
            // Arrange
            var options = new WebSocketOptions { MaxFrameSize = 100 };
            var loggerMock = new Mock<ILogger>();
            var frameHandler = new WebSocketFrameHandler(options, loggerMock.Object);
            string sessionId = "test-session";
            
            // Create and process a partial message
            var fragment = new WebSocketMessage(WebSocketMessageType.Binary, new byte[10], false, 0);
            var result = frameHandler.ProcessFragment(sessionId, fragment);
            Assert.Null(result); // Message is incomplete
            
            // Act
            frameHandler.ClearSession(sessionId);
            
            // Assert - If we send the "final" fragment after clearing, we should not get a complete message
            // that includes the previous fragment (that was cleared)
            var finalFragment = new WebSocketMessage(WebSocketMessageType.Binary, new byte[10], true, 1);
            var newResult = frameHandler.ProcessFragment(sessionId, finalFragment);
            
            // The final fragment is itself a complete message (it has EndOfMessage=true) 
            // so we'll always get a message back, but it should only contain the final fragment's data
            Assert.NotNull(newResult);
            Assert.Equal(10, newResult.Size); // Only contains the final fragment's data (10 bytes)
            Assert.True(newResult.EndOfMessage);
        }
    }
}