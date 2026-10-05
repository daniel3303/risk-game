using System.Threading.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.ResponseCompression;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Risk.Server.Hubs;
using Risk.Server.Matches;
using Risk.Sim.Models;
namespace Risk.Server;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 8192);
        var mapPath = Path.Combine(AppContext.BaseDirectory, "content/classic.json");
        builder.Services.AddSingleton(WorldMap.Parse(File.ReadAllText(mapPath)));
        builder.Services.AddSingleton<RoomRegistry>();
        builder.Services.AddSingleton<RoomCommands>();
        builder.Services.AddSingleton<RoomPublisher>();
        builder.Services.AddSingleton<BotRunner>();
        builder.Services.AddHostedService(p => p.GetRequiredService<BotRunner>());
        builder.Services.AddHostedService<RoomCleanup>();
        builder.Services.AddSingleton<InvocationLimiter>();
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["text/javascript"]);
        });
        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 4096;
            options.MaximumParallelInvocationsPerClient = 1;
            options.AddFilter<InvocationLimiter>();
        }).AddNewtonsoftJsonProtocol(options =>
        {
            options.PayloadSerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            options.PayloadSerializerSettings.Converters.Add(new StringEnumConverter(new CamelCaseNamingStrategy()));
            options.PayloadSerializerSettings.MaxDepth = 16;
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("connections", context => context.Request.Path.StartsWithSegments("/play/negotiate")
                ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ =>
                    new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                : RateLimitPartition.GetNoLimiter("transport"));
        });
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            await next();
        });
        app.UseResponseCompression();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/healthz", () => Results.Text("healthy"));
        app.MapHub<GameHub>("/play").RequireRateLimiting("connections");
        app.MapFallbackToFile("index.html");
        app.Run();
    }
}
