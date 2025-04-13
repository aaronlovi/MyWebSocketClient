using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WebSocketLibrary.Models;
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
        var builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder);
        ConfigureKestrel(builder);

        var app = builder.Build();
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
        builder.Services.AddLogging();
        
        // Add WebSocketLibrary services with custom configuration
        builder.Services.AddWebSocketServices(options => {
            options.Path = WebSocketPath;
            options.IdleTimeoutSeconds = DefaultIdleTimeoutSeconds;
            options.RequireAuthentication = RequireAuthentication;
            options.PingIntervalSeconds = DefaultKeepAliveSeconds;
        });

        // Register our custom WebSocket service that handles application-specific functionality
        builder.Services.AddSingleton<CustomWebSocketService>();
    }

    /// <summary>
    /// Configures the Kestrel server settings.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder</param>
    private static void ConfigureKestrel(WebApplicationBuilder builder) {
        var kestrelConfig = builder.Configuration.GetSection("Kestrel:Endpoints");

        builder.WebHost.ConfigureKestrel((context, options) => {
            var httpEndpoint = kestrelConfig.GetSection("Http:Url").Value;
            var httpsEndpoint = kestrelConfig.GetSection("Https:Url").Value;

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
        app.UseWebSockets();
        
        // Then add our custom WebSocket middleware from WebSocketLibrary
        app.UseWebSocketHandler();

        // Resolve the CustomWebSocketService to initialize it and subscribe to WebSocket events
        app.Services.GetRequiredService<CustomWebSocketService>();

        // Configure a default route that returns a simple message when accessing the root
        app.MapGet("/", () => "WebSocket Server is running. Connect to " + WebSocketPath + " to use WebSockets.");
    }
}
