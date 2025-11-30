using Microsoft.AspNetCore.Mvc;

namespace DiscordBotApi.Middleweres
{
    public class TempAuthentificationMiddlewere
    {
        private readonly RequestDelegate _next;

        public TempAuthentificationMiddlewere(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Query.TryGetValue("pass", out var value) || !value.Contains("poopa"))
            {
                context.Response.ContentType = "application/json";

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "An error occurred while processing your request.",
                    Detail = "User was not recognized"
                };

                await context.Response.WriteAsJsonAsync(problemDetails);
                return;
            }

            await _next(context);
        }
    }
}
