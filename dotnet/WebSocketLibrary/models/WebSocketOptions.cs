using System;

namespace WebSocketLibrary.Models;

/// <summary>
/// Configuration options for WebSocket server.
/// This class allows users to customize WebSocket behavior.
/// </summary>
public class WebSocketOptions
{
    /// <summary>
    /// The maximum size of incoming messages in bytes.
    /// Default is 4KB.
    /// </summary>
    public int MaxMessageSize { get; set; } = 4 * 1024;

    /// <summary>
    /// The idle timeout for client connections in seconds.
    /// If a client is idle for longer than this period, it may be disconnected.
    /// Default is 120 seconds (2 minutes).
    /// </summary>
    public int IdleTimeoutSeconds { get; set; } = 120;
    
    /// <summary>
    /// The ping interval in seconds. The WebSocket server will send
    /// ping frames to clients at this interval to check connection health.
    /// Default is 30 seconds.
    /// </summary>
    public int PingIntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// Flag indicating whether authentication is required for WebSocket connections.
    /// Default is false.
    /// </summary>
    public bool RequireAuthentication { get; set; } = false;
    
    /// <summary>
    /// The path that WebSocket clients should connect to.
    /// Default is "/ws".
    /// </summary>
    public string Path { get; set; } = "/ws";
    
    /// <summary>
    /// The timeout for WebSocket connection operations in seconds.
    /// Default is 10 seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 10;
    
    /// <summary>
    /// Gets the connection timeout as a TimeSpan.
    /// </summary>
    public TimeSpan ConnectionTimeout => TimeSpan.FromSeconds(ConnectionTimeoutSeconds);
    
    /// <summary>
    /// Gets the idle timeout as a TimeSpan.
    /// </summary>
    public TimeSpan IdleTimeout => TimeSpan.FromSeconds(IdleTimeoutSeconds);
    
    /// <summary>
    /// Gets the ping interval as a TimeSpan.
    /// </summary>
    public TimeSpan PingInterval => TimeSpan.FromSeconds(PingIntervalSeconds);
}