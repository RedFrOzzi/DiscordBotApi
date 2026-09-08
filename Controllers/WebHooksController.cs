using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("[controller]")]
public class WebHooksController : ControllerBase
{
    [HttpPost("/update")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> HandleGithubPushWebhook()
    {
        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
            return BadRequest("Missing signature");

        var secret = Environment.GetEnvironmentVariable("WEBHOOK_SECRET");
        if (string.IsNullOrWhiteSpace(secret))
            return StatusCode(500);

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        if (!VerifySignature(body, signature, secret))
        {
            Log.Information("Invalid webhook signature");
            return Unauthorized();
        }

        //Проверка события и ветки
        var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault();
        if (eventType != "push")
            return Ok("Ignored event");

        var json = JsonDocument.Parse(body);
        var branch = json.RootElement.GetProperty("ref").GetString();
        if (branch != "refs/heads/dev")
        {
            Log.Information("Push to {branch}, but we only deploy main", branch);
            return Ok("Branch ignored");
        }

        //Обновление в фоне
        StartDeploymentScript();

        return Ok("Webhook received, deployment started");
    }

    private static bool VerifySignature(string body, string signature, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var computedSignature = "sha256=" + Convert.ToHexString(hash).ToLower();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signature)
        );
    }

    private static void StartDeploymentScript()
    {
        try
        {
            var scriptPath = "/opt/discordbotapi/deploy.sh";
            var startInfo = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"-c \"nohup {scriptPath} > /dev/null 2>&1 &\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(startInfo);
            Log.Information("Deployment script started in background.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start deployment script");
        }
    }
}
