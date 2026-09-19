using DiscordBotApi.Data.AudioTracks;
using DiscordBotApi.Data.YtAudioExtractorData;
using DiscordBotApi.Database;
using DiscordBotApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using YoutubeDLSharp;

namespace DiscordBotApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin, Moderator")]
[Route("yt-extractor")]
public class YtAudioExtractorController(YtAudioExtractorService ytAudioExtractorService, ApplicationDbContext dbContext) : ControllerBase
{
    readonly YtAudioExtractorService _ytAudioExtractor = ytAudioExtractorService;
    readonly ApplicationDbContext _dbContext = dbContext;
    readonly static string _basePath = Path.Combine(AppContext.BaseDirectory, "DataStorage");

    [HttpPost("extract")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ExtractAudioFromYt([FromBody] YtAudioExtractorData ytAudioExtractorData)
    {
        if (ytAudioExtractorData.GuildId <= 0
            || string.IsNullOrWhiteSpace(ytAudioExtractorData.URL)
            || string.IsNullOrWhiteSpace(ytAudioExtractorData.OutputName)
            || ytAudioExtractorData.StartsAt.Equals(ytAudioExtractorData.EndsAt))
        {
            return BadRequest("Incorrect data");
        }

        var guild = _dbContext.Guilds.FirstOrDefault(g => g.Id == ytAudioExtractorData.GuildId);
        if (guild == null)
        {
            return NotFound("Guild was not found");
        }

        var directory = Path.Combine(_basePath, ytAudioExtractorData.GuildId.ToString());

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, $"{ytAudioExtractorData.OutputName}.mp3");

        var track = _dbContext.AudioTracks.FirstOrDefault(at => at.Title == ytAudioExtractorData.OutputName);

        if (track != null)
        {
            return BadRequest(new ProblemDetails()
            {
                Status = StatusCodes.Status409Conflict,
                Detail = "Already exist"
            });
        }

        RunResult<string> res;
        try
        {
            res = await _ytAudioExtractor.DownloadAudioFragmentAsync(ytAudioExtractorData.URL,
                ytAudioExtractorData.StartsAt, ytAudioExtractorData.EndsAt, directory, ytAudioExtractorData.OutputName);
        }
        catch (Exception ex)
        {
            Log.Error("{ex} Error trying to extract audio", ex);

            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (!res.Success)
        {
            Log.Error("Error trying to extract audio: {0}", res.ErrorOutput);

            return StatusCode(StatusCodes.Status500InternalServerError, res.ErrorOutput);
        }

        TimeZoneInfo moscowZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
        DateTime moscowTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, moscowZone);
        var audioTrack = new AudioTrack()
        {
            Title = ytAudioExtractorData.OutputName,
            Path = filePath,
            Guild = guild,
            CreatedAt = moscowTime,
        };

        if (System.IO.File.Exists(filePath))
        {
            audioTrack.SetSize(new FileInfo(filePath).Length);
        }

        _dbContext.AudioTracks.Add(audioTrack);

        if (_dbContext.SaveChanges() == 0)
        {
            Log.Error("Error trying to save audio");

            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Created();
    }
}
