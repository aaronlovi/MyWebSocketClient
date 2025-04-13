# WebSocket Server Library

A library for building WebSocket servers in ASP.NET Core applications. This solution provides a robust, easy-to-use framework for implementing WebSocket functionality in your .NET applications.

## Projects

### WebSocketLibrary

The main library project that provides WebSocket server functionality. It handles connection management, message sending/receiving, broadcasting, and connection health monitoring.

**Key Features:**
- WebSocket connection handling
- Text and binary message support
- Broadcasting to multiple clients
- Connection health monitoring (ping/pong)
- Idle client detection and cleanup
- Authentication support
- Easy integration with ASP.NET Core

### WebSocketLibrary.Tests

Unit tests for the WebSocketLibrary project to ensure functionality works as expected.

### MyWebSocketServer

A sample ASP.NET Core application demonstrating the usage of the WebSocketLibrary.

## Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or another compatible IDE

## Getting Started

### Installing

Add the WebSocketLibrary to your ASP.NET Core project:

```shell
dotnet add reference path/to/WebSocketLibrary.csproj
```

Or install via NuGet (if published):

```shell
dotnet add package WebSocketLibrary
```

### Usage

1. Register WebSocket services in your `Program.cs` or `Startup.cs`:

```csharp
// Add WebSocket services with custom options
builder.Services.AddWebSocketServices(options => 
{
    options.Path = "/ws"; // WebSocket endpoint path
    options.IdleTimeoutSeconds = 120; // Idle timeout in seconds
    options.RequireAuthentication = false; // Whether authentication is required
    options.PingIntervalSeconds = 30; // Interval for sending ping frames
});
```

2. Configure WebSocket middleware in your application pipeline:

```csharp
// Add WebSocket middleware
app.UseWebSockets();
```

3. Handle WebSocket events:

```csharp
// Inject IWebSocketHandler in your service
public class MyService
{
    private readonly IWebSocketHandler _webSocketHandler;

    public MyService(IWebSocketHandler webSocketHandler)
    {
        _webSocketHandler = webSocketHandler;
        
        // Subscribe to WebSocket events
        _webSocketHandler.ClientConnected += OnClientConnected;
        _webSocketHandler.ClientDisconnected += OnClientDisconnected;
        _webSocketHandler.MessageReceived += OnMessageReceived;
    }
    
    private void OnClientConnected(object sender, WebSocketClientSession session)
    {
        // Handle new client connection
    }
    
    private void OnClientDisconnected(object sender, WebSocketClientSession session)
    {
        // Handle client disconnection
    }
    
    private void OnMessageReceived(object sender, (WebSocketClientSession session, WebSocketMessage message) data)
    {
        // Process received message
        var (session, message) = data;
        
        if (message.MessageType == WebSocketMessageType.Text)
        {
            string text = message.GetTextContent();
            // Process text message
        }
    }
    
    // Send a message to a specific client
    public Task SendMessageAsync(string sessionId, string message)
    {
        var webSocketMessage = WebSocketMessage.CreateTextMessage(message);
        return _webSocketHandler.SendMessageAsync(sessionId, webSocketMessage, CancellationToken.None);
    }
    
    // Broadcast a message to all connected clients
    public Task BroadcastMessageAsync(string message)
    {
        var webSocketMessage = WebSocketMessage.CreateTextMessage(message);
        return _webSocketHandler.BroadcastMessageAsync(webSocketMessage, CancellationToken.None);
    }
}
```

## Building

```shell
dotnet build
```

## Running the tests

```shell
dotnet test
```

## License

This project is licensed under a restrictive license with all rights reserved. Contact Aaron Lovi (aaronlovi@gmail.com) for permissions to use, modify, or distribute this software. See the [LICENSE](LICENSE) file for details.