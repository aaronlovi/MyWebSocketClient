using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using WebSocketLibrary.Utilities;

namespace MyWebSocketServer;

/// <summary>
/// The main entry point for the MyWebSocketServer application.
/// This application demonstrates the use of the WebSocketLibrary.
/// </summary>
public class Program {
    private const int DefaultHttpPort = 5000;
    private const int DefaultHttpsPort = 5001;
    private const int DefaultKeepAliveSeconds = 5;
    private const int DefaultIdleTimeoutSeconds = 120;
    private const bool RequireAuthentication = false;
    private const string WebSocketPath = "/ws";

    /// <summary>
    /// The application's entry point.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    public static void Main(string[] args) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder);
        ConfigureKestrel(builder);

        WebApplication app = builder.Build();
        ConfigureMiddleWare(app);

        Console.WriteLine("Server started - accepting connections on path " + WebSocketPath);
        app.Run();
    }

    /// <summary>
    /// Configures the application's services.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder</param>
    private static void ConfigureServices(WebApplicationBuilder builder) {
        // Add logging
        _ = builder.Services.AddLogging();

        // Add WebSocketLibrary services with custom configuration
        _ = builder.Services.AddWebSocketServices(options => {
            options.Path = WebSocketPath;
            options.IdleTimeoutSeconds = DefaultIdleTimeoutSeconds;
            options.RequireAuthentication = RequireAuthentication;
            options.PingIntervalSeconds = DefaultKeepAliveSeconds;
        });

        // Register our custom WebSocket service that handles application-specific functionality
        _ = builder.Services.AddSingleton<CustomWebSocketService>();
    }

    /// <summary>
    /// Configures the Kestrel server settings.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder</param>
    private static void ConfigureKestrel(WebApplicationBuilder builder) {
        Microsoft.Extensions.Configuration.IConfigurationSection kestrelConfig = builder.Configuration.GetSection("Kestrel:Endpoints");

        _ = builder.WebHost.ConfigureKestrel((context, options) => {
            string? httpEndpoint = kestrelConfig.GetSection("Http:Url").Value;
            string? httpsEndpoint = kestrelConfig.GetSection("Https:Url").Value;

            if (!string.IsNullOrEmpty(httpEndpoint)) {
                options.ListenAnyIP(new Uri(httpEndpoint).Port);
            } else {
                options.ListenAnyIP(DefaultHttpPort);
            }

            if (!string.IsNullOrEmpty(httpsEndpoint)) {
                options.ListenAnyIP(new Uri(httpsEndpoint).Port, listenOptions => listenOptions.UseHttps());
            } else {
                options.ListenAnyIP(DefaultHttpsPort, listenOptions => listenOptions.UseHttps()); // Default HTTPS port
            }
        });
    }

    /// <summary>
    /// Configures the application's middleware pipeline.
    /// </summary>
    /// <param name="app">The WebApplication</param>
    private static void ConfigureMiddleWare(WebApplication app) {
        // First add the standard ASP.NET Core WebSockets middleware
        _ = app.UseWebSockets();

        // Then add our custom WebSocket middleware from WebSocketLibrary
        _ = app.UseWebSocketHandler();

        // Resolve the CustomWebSocketService to initialize it and subscribe to WebSocket events
        _ = app.Services.GetRequiredService<CustomWebSocketService>();

        // Configure a default route that returns a simple message when accessing the root
        _ = app.MapGet("/", () => "WebSocket Server is running. Connect to " + WebSocketPath + " to use WebSockets.");
    }
}
