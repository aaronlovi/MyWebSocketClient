using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WebSocketLibrary.Contracts;
using WebSocketLibrary.Models;
using WebSocketLibrary.Services;
using Xunit;

namespace WebSocketLibrary.Tests;

/// <summary>
/// Unit tests for the WebSocketHandler class.
/// </summary>
public class WebSocketHandlerTests
{
    private readonly Mock<ILogger<WebSocketHandler>> _mockLogger;
    private readonly Mock<IOptions<WebSocketOptions>> _mockOptions;
    private readonly Mock<WebSocket> _mockWebSocket;
    private readonly Mock<HttpContext> _mockHttpContext;
    private readonly WebSocketOptions _options;
    private readonly IWebSocketHandler _webSocketHandler;

    /// <summary>
    /// Initializes a new instance of the WebSocketHandlerTests class.
    /// Sets up common test dependencies.
    /// </summary>
    public WebSocketHandlerTests()
    {
        _mockLogger = new Mock<ILogger<WebSocketHandler>>();
        _options = new WebSocketOptions();
        _mockOptions = new Mock<IOptions<WebSocketOptions>>();
        _mockOptions.Setup(o => o.Value).Returns(_options);
        _mockWebSocket = new Mock<WebSocket>();
        _mockHttpContext = new Mock<HttpContext>();

        _webSocketHandler = new WebSocketHandler(_mockOptions.Object, _mockLogger.Object);
    }

    /// <summary>
    /// Tests that SendMessageAsync throws ArgumentNullException when sessionId is null.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_NullSessionId_ThrowsArgumentNullException()
    {
        // Arrange
        var message = WebSocketMessage.CreateTextMessage("Test message");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _webSocketHandler.SendMessageAsync(null, message, CancellationToken.None));
    }

    /// <summary>
    /// Tests that SendMessageAsync throws ArgumentNullException when message is null.
    /// </summary>
    [Fact]
    public async Task SendMessageAsync_NullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _webSocketHandler.SendMessageAsync("test-session", null, CancellationToken.None));
    }

    /// <summary>
    /// Tests that BroadcastMessageAsync throws ArgumentNullException when message is null.
    /// </summary>
    [Fact]
    public async Task BroadcastMessageAsync_NullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _webSocketHandler.BroadcastMessageAsync(null, CancellationToken.None));
    }

    /// <summary>
    /// Tests that DisconnectClientAsync throws ArgumentNullException when sessionId is null.
    /// </summary>
    [Fact]
    public async Task DisconnectClientAsync_NullSessionId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _webSocketHandler.DisconnectClientAsync(null, WebSocketCloseStatus.NormalClosure, "Test", CancellationToken.None));
    }

    /// <summary>
    /// Tests that HandleConnectionAsync throws ArgumentNullException when webSocket is null.
    /// </summary>
    [Fact]
    public async Task HandleConnectionAsync_NullWebSocket_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _webSocketHandler.HandleConnectionAsync(_mockHttpContext.Object, null, CancellationToken.None));
    }

    /// <summary>
    /// Tests that the ClientConnected event is raised when a client connects.
    /// </summary>
    [Fact]
    public async Task HandleConnectionAsync_ClientConnects_RaisesClientConnectedEvent()
    {
        // Arrange
        _mockWebSocket.Setup(ws => ws.State).Returns(WebSocketState.Open);
        _mockWebSocket.Setup(ws => ws.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        
        WebSocketClientSession capturedSession = null;
        _webSocketHandler.ClientConnected += (sender, session) => capturedSession = session;

        try
        {
            // Act
            var task = _webSocketHandler.HandleConnectionAsync(_mockHttpContext.Object, _mockWebSocket.Object, CancellationToken.None);
            
            // Wait a short time for event to be raised
            await Task.Delay(100);
            
            // Assert
            Assert.NotNull(capturedSession);
            Assert.Equal(WebSocketState.Open, capturedSession.WebSocket.State);
        }
        finally
        {
            // Force task to complete
            _mockWebSocket.Setup(ws => ws.State).Returns(WebSocketState.Closed);
        }
    }
    
    /// <summary>
    /// Tests that WebSocketMessage.CreateTextMessage correctly creates a text message.
    /// </summary>
    [Fact]
    public void CreateTextMessage_ValidInput_CreatesCorrectMessage()
    {
        // Arrange
        string text = "Test message";
        
        // Act
        var message = WebSocketMessage.CreateTextMessage(text);
        
        // Assert
        Assert.Equal(WebSocketMessageType.Text, message.MessageType);
        Assert.Equal(text, message.GetTextContent());
        Assert.True(message.EndOfMessage);
    }
    
    /// <summary>
    /// Tests that WebSocketMessage.CreateBinaryMessage correctly creates a binary message.
    /// </summary>
    [Fact]
    public void CreateBinaryMessage_ValidInput_CreatesCorrectMessage()
    {
        // Arrange
        byte[] data = new byte[] { 1, 2, 3, 4 };
        
        // Act
        var message = WebSocketMessage.CreateBinaryMessage(data);
        
        // Assert
        Assert.Equal(WebSocketMessageType.Binary, message.MessageType);
        Assert.Same(data, message.Data);
        Assert.True(message.EndOfMessage);
    }
}