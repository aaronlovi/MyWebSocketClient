using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebSocketLibrary.Contracts;
using WebSocketLibrary.Models;

namespace WebSocketLibrary.Utilities;

/// <summary>
/// ASP.NET Core middleware for handling WebSocket connections.
/// </summary>
public class WebSocketMiddleware {
    private readonly RequestDelegate _next;
    private readonly IWebSocketHandler _webSocketHandler;
    private readonly WebSocketOptions _options;
    private readonly ILogger<WebSocketMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="webSocketHandler">The WebSocket handler implementation</param>
    /// <param name="options">WebSocket configuration options</param>
    /// <param name="logger">Logger for the middleware</param>
    public WebSocketMiddleware(
        RequestDelegate next,
        IWebSocketHandler webSocketHandler,
        WebSocketOptions options,
        ILogger<WebSocketMiddleware> logger) {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _webSocketHandler = webSocketHandler ?? throw new ArgumentNullException(nameof(webSocketHandler));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes an HTTP request to determine if it's a WebSocket request.
    /// </summary>
    /// <param name="context">The HTTP context for this request</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task InvokeAsync(HttpContext context) {
        // Check if this is a WebSocket request
        if (context.Request.Path == _options.Path) {
            if (context.WebSockets.IsWebSocketRequest) {
                _logger.LogInformation("WebSocket request received at {Path}", _options.Path);

                // Authenticate if required
                if (_options.RequireAuthentication && (context.User?.Identity?.IsAuthenticated != true)) {
                    _logger.LogWarning("Unauthenticated WebSocket connection attempt rejected");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                // Accept the WebSocket connection
                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                _logger.LogInformation("WebSocket connection established");

                // Handle the WebSocket connection
                await _webSocketHandler.HandleConnectionAsync(context, webSocket, context.RequestAborted);
                return;
            }

            // If it's not a WebSocket request but matches our path
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Not a WebSocket request path, continue with the pipeline
        await _next(context);
    }
}