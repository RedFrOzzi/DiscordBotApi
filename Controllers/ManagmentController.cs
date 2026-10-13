using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static DiscordBotApi.Utilities.LogsParser;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("managment")]
public class ManagmentController : ControllerBase
{
    readonly string _logsPath;

    public ManagmentController()
    {
        var fullPath = Environment.GetEnvironmentVariable("LOGS_PATH");
        if (!string.IsNullOrWhiteSpace(fullPath))
        {
            var k = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(k))
            {
                _logsPath = k;
                return;
            }
        }

        _logsPath = "Logs";
    }

    [HttpPost("update")]
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

    [HttpPost("update-server")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> UpdateServer()
    {
        StartDeploymentScript();

        return Ok();
    }

    [HttpGet("logs")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetServerLogs(
        [FromQuery] string? search,
        [FromQuery] string? level,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? cursor = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        var offset = Cursor.Decode(cursor);

        var file = Directory.GetFiles(_logsPath, "app-log-*.txt")
                            .OrderByDescending(System.IO.File.GetLastWriteTimeUtc)
                            .FirstOrDefault();

        if (file is null) return NotFound(new LogPage([], null, false));

        try
        {
            var reader = new LogFileReader(file);

            var hits = new List<LogEntry>();
            var skipped = 0;
            var hasMore = false;

            foreach (var entry in reader.ReadBackwards())
            {
                if (from is not null && entry.Ts < from)
                    break;

                if (to is not null && entry.Ts > to)
                    continue;

                if (!string.IsNullOrEmpty(level) &&
                    !string.Equals(entry.Level, level, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(search) &&
                    entry.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (skipped < offset)
                {
                    skipped++;
                    continue;
                }

                if (hits.Count < pageSize)
                {
                    hits.Add(entry);
                    continue;
                }

                hasMore = true;
                break;
            }

            var next = hasMore ? Cursor.Encode(offset + hits.Count) : null;
            return Ok(new LogPage(hits, next, hasMore));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to read logs");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
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
