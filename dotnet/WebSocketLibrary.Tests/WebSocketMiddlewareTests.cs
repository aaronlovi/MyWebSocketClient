using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using WebSocketLibrary.Contracts;
using WebSocketLibrary.Models;
using WebSocketLibrary.Utilities;
using Xunit;

namespace WebSocketLibrary.Tests
{
    /// <summary>
    /// Unit tests for the WebSocketMiddleware class.
    /// </summary>
    public class WebSocketMiddlewareTests
    {
        private readonly Mock<IWebSocketHandler> _mockWebSocketHandler;
        private readonly Mock<ILogger<WebSocketMiddleware>> _mockLogger;
        private readonly Mock<HttpContext> _mockHttpContext;
        private readonly Mock<HttpRequest> _mockHttpRequest;
        private readonly Mock<HttpResponse> _mockHttpResponse;
        private readonly Mock<WebSocketManager> _mockWebSocketManager;
        private readonly Mock<WebSocket> _mockWebSocket;
        private readonly WebSocketOptions _options;
        private readonly WebSocketMiddleware _middleware;
        private bool _nextCalled;

        /// <summary>
        /// Initializes a new instance of the WebSocketMiddlewareTests class.
        /// Sets up common test dependencies.
        /// </summary>
        public WebSocketMiddlewareTests()
        {
            _mockWebSocketHandler = new Mock<IWebSocketHandler>();
            _mockLogger = new Mock<ILogger<WebSocketMiddleware>>();
            _mockHttpContext = new Mock<HttpContext>();
            _mockHttpRequest = new Mock<HttpRequest>();
            _mockHttpResponse = new Mock<HttpResponse>();
            _mockWebSocketManager = new Mock<WebSocketManager>();
            _mockWebSocket = new Mock<WebSocket>();
            
            _options = new WebSocketOptions
            {
                Path = "/ws"
            };
            
            _nextCalled = false;
            
            RequestDelegate next = (context) =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            };
            
            _middleware = new WebSocketMiddleware(next, _mockWebSocketHandler.Object, _options, _mockLogger.Object);
            
            _mockHttpContext.Setup(c => c.Request).Returns(_mockHttpRequest.Object);
            _mockHttpContext.Setup(c => c.Response).Returns(_mockHttpResponse.Object);
            _mockHttpContext.Setup(c => c.WebSockets).Returns(_mockWebSocketManager.Object);
        }

        /// <summary>
        /// Tests that the middleware passes to next if the request path doesn't match.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_PathDoesNotMatch_CallsNext()
        {
            // Arrange
            _mockHttpRequest.Setup(r => r.Path).Returns("/not-ws");
            
            // Act
            await _middleware.InvokeAsync(_mockHttpContext.Object);
            
            // Assert
            Assert.True(_nextCalled);
            _mockWebSocketManager.Verify(m => m.IsWebSocketRequest, Times.Never);
        }

        /// <summary>
        /// Tests that the middleware returns 400 Bad Request when the path matches but it's not a WebSocket request.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_PathMatchesNotWebSocketRequest_Returns400()
        {
            // Arrange
            _mockHttpRequest.Setup(r => r.Path).Returns(_options.Path);
            _mockWebSocketManager.Setup(m => m.IsWebSocketRequest).Returns(false);
            
            // Act
            await _middleware.InvokeAsync(_mockHttpContext.Object);
            
            // Assert
            Assert.False(_nextCalled);
            _mockHttpResponse.VerifySet(r => r.StatusCode = StatusCodes.Status400BadRequest);
        }

        /// <summary>
        /// Tests that the middleware accepts and handles WebSocket requests when the path matches.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_ValidWebSocketRequest_AcceptsAndHandlesWebSocket()
        {
            // Arrange
            _mockHttpRequest.Setup(r => r.Path).Returns(_options.Path);
            _mockWebSocketManager.Setup(m => m.IsWebSocketRequest).Returns(true);
            _mockWebSocketManager.Setup(m => m.AcceptWebSocketAsync()).ReturnsAsync(_mockWebSocket.Object);
            
            // Act
            await _middleware.InvokeAsync(_mockHttpContext.Object);
            
            // Assert
            Assert.False(_nextCalled);
            _mockWebSocketManager.Verify(m => m.AcceptWebSocketAsync(), Times.Once);
            _mockWebSocketHandler.Verify(h => h.HandleConnectionAsync(
                _mockHttpContext.Object, 
                _mockWebSocket.Object, 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        /// <summary>
        /// Tests that the middleware returns 401 Unauthorized when authentication is required but not provided.
        /// </summary>
        [Fact]
        public async Task InvokeAsync_RequiresAuthNoAuth_Returns401()
        {
            // Arrange
            _options.RequireAuthentication = true;
            
            _mockHttpRequest.Setup(r => r.Path).Returns(_options.Path);
            _mockWebSocketManager.Setup(m => m.IsWebSocketRequest).Returns(true);
            
            var mockIdentity = new Mock<System.Security.Principal.IIdentity>();
            mockIdentity.Setup(i => i.IsAuthenticated).Returns(false);
            
            _mockHttpContext.Setup(c => c.User.Identity).Returns(mockIdentity.Object);
            
            // Act
            await _middleware.InvokeAsync(_mockHttpContext.Object);
            
            // Assert
            Assert.False(_nextCalled);
            _mockHttpResponse.VerifySet(r => r.StatusCode = StatusCodes.Status401Unauthorized);
            _mockWebSocketManager.Verify(m => m.AcceptWebSocketAsync(), Times.Never);
        }
    }
}