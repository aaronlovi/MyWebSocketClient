using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebSockets;

namespace MyWebSocketServer;

public class Program {
    private const int DefaultHttpPort = 5000;
    private const int DefaultHttpsPort = 5001;
    private const int DefaultKeepAliveSeconds = 5;

    public static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureWebSocketOptions(builder);
        ConfigureKestrel(builder);

        var app = builder.Build();
        ConfigureMiddleWare(app);

        Console.WriteLine("Server started - accepting connections");
        app.Run();
    }

    private static void ConfigureWebSocketOptions(WebApplicationBuilder builder) {
        builder.Services.AddWebSockets(options => {
            options.KeepAliveInterval = TimeSpan.FromSeconds(DefaultKeepAliveSeconds);
        });
    }

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

    private static void ConfigureMiddleWare(WebApplication app) {
        app.UseWebSockets();
        app.Use(WebSocketMiddleware);
    }

    private static async Task WebSocketMiddleware(HttpContext context, Func<Task> next) {
        if (context.WebSockets.IsWebSocketRequest) {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await WebSocketHandler.HandleAsync(context, webSocket);
        } else {
            await next();
        }
    }
}
