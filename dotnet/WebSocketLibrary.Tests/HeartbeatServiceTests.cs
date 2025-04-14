using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using WebSocketLibrary.Services;
using WebSocketLibrary.Models;
using Xunit;
using Moq;
using WebSocketLibrary.services;

namespace WebSocketLibrary.Tests;

/// <summary>
/// Unit tests for the HeartbeatService class.
/// </summary>
public class HeartbeatServiceTests
{
    /// <summary>
    /// Verifies that StartAsync sends ping frames at the configured interval.
    /// </summary>
    [Fact]
    public async Task StartAsync_SendsPingAtConfiguredInterval()
    {
        // Arrange
        var pingInterval = TimeSpan.FromMilliseconds(100);
        var timeoutThreshold = TimeSpan.FromSeconds(5);
        var heartbeatService = new HeartbeatService(pingInterval, timeoutThreshold);
        var mockWebSocket = new Mock<WebSocket>();
        // Ensure the mock WebSocket is properly configured to simulate an open state
        mockWebSocket.Setup(ws => ws.State).Returns(WebSocketState.Open);
        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        Task task = heartbeatService.StartAsync(mockWebSocket.Object, cancellationTokenSource.Token);
        // Use a longer delay to ensure the test has enough time to verify pings
        await Task.Delay(1000); // Allow sufficient time for pings to be sent
        cancellationTokenSource.Cancel();
        await task;

        // Assert
        mockWebSocket.Verify(ws => ws.SendAsync(
            It.IsAny<ArraySegment<byte>>(),
            WebSocketMessageType.Binary,
            true,
            It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    /// <summary>
    /// Verifies that IsClientUnresponsive returns true when the client exceeds the timeout threshold.
    /// </summary>
    [Fact]
    public void IsClientUnresponsive_ReturnsTrue_WhenClientExceedsTimeout()
    {
        // Arrange
        var pingInterval = TimeSpan.FromMilliseconds(100);
        var timeoutThreshold = TimeSpan.FromSeconds(5);
        var heartbeatService = new HeartbeatService(pingInterval, timeoutThreshold);
        var clientSession = new WebSocketClientSession("test-session", new Mock<WebSocket>().Object, DateTime.UtcNow - TimeSpan.FromSeconds(10));

        // Act
        bool result = heartbeatService.IsClientUnresponsive(clientSession);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Verifies that DisconnectUnresponsiveClientAsync closes the WebSocket when the client is unresponsive.
    /// </summary>
    [Fact]
    public async Task DisconnectUnresponsiveClientAsync_ClosesWebSocket_WhenClientIsUnresponsive()
    {
        // Arrange
        var pingInterval = TimeSpan.FromMilliseconds(100);
        var timeoutThreshold = TimeSpan.FromSeconds(5);
        var heartbeatService = new HeartbeatService(pingInterval, timeoutThreshold);
        var mockWebSocket = new Mock<WebSocket>();
        _ = mockWebSocket.Setup(ws => ws.State).Returns(WebSocketState.Open);
        var clientSession = new WebSocketClientSession("test-session", mockWebSocket.Object, DateTime.UtcNow - TimeSpan.FromSeconds(10));

        // Act
        await heartbeatService.DisconnectUnresponsiveClientAsync(clientSession);

        // Assert
        mockWebSocket.Verify(ws => ws.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Client unresponsive",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
