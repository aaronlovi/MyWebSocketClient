using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebSocketLibrary.Contracts;
using WebSocketLibrary.Services;
using WebSocketLibrary.services;

namespace WebSocketLibrary.Utilities
{
    /// <summary>
    /// Extension methods for adding and configuring WebSocket support in ASP.NET Core applications.
    /// </summary>
    public static class WebSocketExtensions {
        /// <summary>
        /// Adds WebSocket services to the service collection.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureOptions">Optional delegate to configure WebSocket options</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddWebSocketServices(
            this IServiceCollection services,
            Action<Models.WebSocketOptions>? configureOptions = null) {
            // Configure options
            _ = configureOptions != null
                ? services.Configure(configureOptions)
                : services.Configure<Models.WebSocketOptions>(_ => { });

            // Register singleton instance of WebSocketHandler
            _ = services.AddSingleton<IWebSocketHandler, WebSocketHandler>();

            // Register HeartbeatService and its options
            _ = services.Configure<HeartbeatServiceOptions>(_ => { });
            _ = services.AddSingleton<HeartbeatService>();

            return services;
        }

        /// <summary>
        /// Adds WebSocket middleware to the application pipeline.
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <returns>The application builder for chaining</returns>
        public static IApplicationBuilder UseWebSocketHandler(this IApplicationBuilder app) {
            // Add our WebSocketLibrary options
            Models.WebSocketOptions options = app.ApplicationServices
                .GetRequiredService<IOptions<Models.WebSocketOptions>>().Value;

            // Add our custom WebSocket middleware
            _ = app.UseMiddleware<WebSocketMiddleware>(
                app.ApplicationServices.GetRequiredService<IWebSocketHandler>(),
                options);

            return app;
        }
    }
}