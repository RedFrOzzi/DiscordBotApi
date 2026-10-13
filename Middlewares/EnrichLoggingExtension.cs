using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;

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

        var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User?.Identity?.Name
            ?? "Anonymous";
       
        diagnosticContext.Set("UserId", userId);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
    }
}
