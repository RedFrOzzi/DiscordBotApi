using Serilog;

namespace DiscordBotApi.Middlewares;

public static class EnrichLoggingExtension
{
    public static void AddLoggingEnrichments(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var diagnosticContext = context.RequestServices.GetRequiredService<IDiagnosticContext>();

            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(remoteIp) || remoteIp == "::1")
                remoteIp = "localhost";
            diagnosticContext.Set("ClientIP", remoteIp);

            var user = context.User?.Identity?.Name ?? "Anonymous";
            diagnosticContext.Set("UserName", user);
            
            diagnosticContext.Set("UserAgent", context.Request.Headers.UserAgent.FirstOrDefault());

            await next();
        });
    }
}
