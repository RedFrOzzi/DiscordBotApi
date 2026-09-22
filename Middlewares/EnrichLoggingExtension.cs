using Serilog;
using System.Net;

namespace DiscordBotApi.Middlewares;

public static class EnrichLoggingExtension
{
    public static void EnrichLogging(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
        var remoteIp = remoteIpAddress != null && IPAddress.IsLoopback(remoteIpAddress)
        ? "localhost"
        : remoteIpAddress?.ToString() ?? "unknown";

        diagnosticContext.Set("ClientIP", remoteIp);

        var user = httpContext.User?.Identity?.Name ?? "Anonymous";
        diagnosticContext.Set("UserName", user);

        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
    }
}
