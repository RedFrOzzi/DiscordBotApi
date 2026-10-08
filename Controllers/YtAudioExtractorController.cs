using DiscordBotApi.Data.AudioExtractionStates;
using DiscordBotApi.Data.AudioTracks;
using DiscordBotApi.Data.YtAudioExtractorDatas;
using DiscordBotApi.Database;
using DiscordBotApi.Services;
using DiscordBotApi.Utilities.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;
using Serilog;
using System.Diagnostics;
using System.Text.RegularExpressions;
using YoutubeDLSharp;

namespace DiscordBotApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin, Moderator")]
[Route("yt-extractor")]
public partial class YtAudioExtractorController(
    YtAudioExtractorService ytAudioExtractorService,
    ApplicationDbContext dbContext,
    IServiceScopeFactory scopeFactory) : ControllerBase
{
    readonly YtAudioExtractorService _ytAudioExtractor = ytAudioExtractorService;
    readonly ApplicationDbContext _dbContext = dbContext;
    readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    const string _checkExtractionStatusEndpoint = "check-extraction";
    const string _getExtractedAudioEndpoint = "get-extracted";
    readonly static string _basePath = Path.Combine(AppContext.BaseDirectory, "DataStorage");
    readonly static string _audioExtractionDirectory = Path.Combine(Path.Combine(AppContext.BaseDirectory, "DataStorage"), "TempAudio");
    readonly static string _checkStatusUrl = $"yt-extractor/{_checkExtractionStatusEndpoint}";
    readonly static string _getExtractedAudioUrl = $"yt-extractor/{_getExtractedAudioEndpoint}";
    const long _reservedFreeSpace = 536900000; //0.5 gb
    const int _maxTitleLength = 79;

    [HttpPost("extract")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ExtractAudioFromYt([FromBody] YtAudioExtractorData ytAudioExtractorData)
    {
        if (ytAudioExtractorData.GuildId <= 1
            || string.IsNullOrWhiteSpace(ytAudioExtractorData.URL)
            || string.IsNullOrWhiteSpace(ytAudioExtractorData.OutputName)
            || ytAudioExtractorData.StartsAt.Equals(ytAudioExtractorData.EndsAt)
            || ytAudioExtractorData.StartsAt > ytAudioExtractorData.EndsAt)
        {
            return BadRequest("Incorrect data");
        }

        if (ytAudioExtractorData.OutputName.Length > _maxTitleLength || !LettersNumbersSymbolsRegex().IsMatch(ytAudioExtractorData.OutputName))
            return BadRequest("Incorrect format of Output name");

        var freeSpace = new DriveInfo(AppContext.BaseDirectory).AvailableFreeSpace;
        if (freeSpace < _reservedFreeSpace)
            return BadRequest("Не достаточно место на диске");

        var guild = _dbContext.Guilds.FirstOrDefault(g => g.Id == ytAudioExtractorData.GuildId);
        if (guild == null)
        {
            return NotFound("Guild was not found");
        }

        var directory = Path.Combine(_basePath, ytAudioExtractorData.GuildId.ToString());

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, $"{ytAudioExtractorData.OutputName}.mp3");

        var track = _dbContext.AudioTracks
            .AsNoTracking()
            .Where(t => t.Guild.Id == ytAudioExtractorData.GuildId)
            .FirstOrDefault(at => at.Title == ytAudioExtractorData.OutputName);

        if (track != null)
        {
            return Conflict(new ProblemDetails()
            {
                Status = StatusCodes.Status409Conflict,
                Detail = " Audio track already exist"
            });
        }

        RunResult<string> res;
        try
        {
            res = await _ytAudioExtractor.DownloadExactAudioFragmentAsync(ytAudioExtractorData.URL,
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

            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        TimeZoneInfo moscowZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
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
            Log.Error("Error trying to save audio in database");
            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete file on databse failure");
            }

            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Created();
    }


    [HttpPost("begin-extraction")]
    [ProducesResponseType<BeginExtractionResponseDto>(202)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public IActionResult CreateAudioExtractionProcess([FromBody] BeginAudioExtractionDto beginAudioExtractionDto)
    {
        var freeSpace = new DriveInfo(AppContext.BaseDirectory).AvailableFreeSpace;
        if (freeSpace < _reservedFreeSpace)
            return BadRequest("Не достаточно место на диске");

        var requestId = Guid.NewGuid();

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AudioExtractionState.Add(new AudioExtractionState
            {
                RequestId = requestId,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
            });

            if (db.SaveChanges() == 0)
            {
                return Problem("Error on creating database record",
                    HttpContext.Request.Path,
                    StatusCodes.Status500InternalServerError,
                    "Internal Server Error");
            }
        }

        _ = Task.Run(() => RunExtractionAsync(_audioExtractionDirectory, requestId, beginAudioExtractionDto), CancellationToken.None);

        return Accepted(new BeginExtractionResponseDto 
        {
            StatusUrl = _checkStatusUrl,
            OperationId = requestId.ToString(),
        });
    }

    [HttpGet(_checkExtractionStatusEndpoint)]
    [ProducesResponseType<AudioExtractionStateGetDto>(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public IActionResult CheckAudioExtractionProcess([FromQuery] Guid operationId)
    {
        if (operationId == Guid.Empty)
        {
            return BadRequest();
        }

        var state = _dbContext.AudioExtractionState.FirstOrDefault(s => s.RequestId == operationId);

        if (state == null)
        {
            return NotFound();
        }

        return Ok(new AudioExtractionStateGetDto()
        {
            Status = state.Status,
            ResultMessage = state.ResultMessage,
            GetExtractedAudioUrl = _getExtractedAudioUrl
        });
    }

    [HttpGet(_getExtractedAudioEndpoint)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetExtractedAudio([FromQuery] Guid operationId)
    {
        if (operationId == Guid.Empty)
        {
            return Problem("Operation ID was empty", HttpContext.Request.Path, StatusCodes.Status400BadRequest);
        }

        var state = _dbContext.AudioExtractionState.FirstOrDefault(s => s.RequestId == operationId);

        if (state == null)
        {
            return Problem("Status record not found", HttpContext.Request.Path, StatusCodes.Status404NotFound);
        }

        if (!string.Equals(state.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return Problem("Operation doesn't mark as successful", HttpContext.Request.Path, StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(state.Name))
        {
            return Problem("Audio created has incorrect name", HttpContext.Request.Path, StatusCodes.Status500InternalServerError);
        }

        var audioPath =Path.Combine(_audioExtractionDirectory, $"{state.Name}.mp3");
        if (!System.IO.File.Exists(audioPath))
            return Problem("Audio not found", HttpContext.Request.Path, StatusCodes.Status404NotFound);

        var stream = System.IO.File.OpenRead(audioPath);
        return File(stream, "audio/mpeg", fileDownloadName: $"{state.Name}.mp3", enableRangeProcessing: true);
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UploadAudioDirectly([FromForm] AudioEditDto audioTrackDto)
    {
        if (audioTrackDto.GuildId <= 1
            || string.IsNullOrWhiteSpace(audioTrackDto.Title)
            || audioTrackDto.StartsAt.Equals(audioTrackDto.EndsAt)
            || audioTrackDto.StartsAt > audioTrackDto.EndsAt
            || audioTrackDto.OperationId == Guid.Empty)
        {
            return BadRequest("Incorrect data");
        }

        if (audioTrackDto.Title.Length > _maxTitleLength || !LettersNumbersSymbolsRegex().IsMatch(audioTrackDto.Title))
            return BadRequest("Incorrect format of Title");

        var state = _dbContext.AudioExtractionState
            .AsNoTracking()
            .FirstOrDefault(s => s.RequestId == audioTrackDto.OperationId);

        if (state == null)
            return Problem("Operation with this id was not found", HttpContext.Request.Path, StatusCodes.Status404NotFound);

        if (!string.Equals(state.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            return Problem("Operation is not marked as succeeded", HttpContext.Request.Path, StatusCodes.Status400BadRequest);

        var guild = _dbContext.Guilds
            .FirstOrDefault(g => g.Id == audioTrackDto.GuildId);

        if (guild == null)
            return Problem("Guild with this id was not found", HttpContext.Request.Path, StatusCodes.Status404NotFound);

        if (_dbContext.AudioTracks
            .Where(t => t.Guild.Id == guild.Id)
            .Any(t => t.Title == audioTrackDto.Title))
            return Problem("Track with this tiltle already exist", HttpContext.Request.Path, StatusCodes.Status409Conflict);

        var directory = Path.Combine(_basePath, guild.Id.ToString());

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var audioInputPath = Path.Combine(_audioExtractionDirectory, $"{state.Name}.mp3");

        if (!System.IO.File.Exists(audioInputPath))
            return Problem("Input file was not found", HttpContext.Request.Path, StatusCodes.Status404NotFound);

        string fileOutputPath = Path.Combine(directory, $"{audioTrackDto.Title}.mp3");
        var start = TimeSpan.FromMilliseconds(audioTrackDto.StartsAt.TotalMilliseconds);
        TimeSpan? end = audioTrackDto.EndsAt.IsTillTheEnd
        ? null
        : TimeSpan.FromMilliseconds(audioTrackDto.EndsAt.TotalMilliseconds);

        try
        {
            var res = await _ytAudioExtractor.CutAndSaveAudioAsync(audioInputPath, fileOutputPath, start, end);
            if (res is Error error)
            {
                Log.Error("Audio extraction failed with message: {0}", error.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, error.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Audio extraction failed with message: {0}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, "Audio processing failed");
        }

        TimeZoneInfo moscowZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
        DateTime moscowTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, moscowZone);
        var track = new AudioTrack()
        {
            Title = audioTrackDto.Title,
            Path = fileOutputPath,
            Guild = guild,
            CreatedAt = moscowTime,
        };

        if (!System.IO.File.Exists(fileOutputPath))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save file");
        }

        track.SetSize(new FileInfo(fileOutputPath).Length);

        _dbContext.AudioTracks.Add(track);

        if (_dbContext.SaveChanges() == 0)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save to database");
        }

        try
        {
            System.IO.File.Delete(audioInputPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to delete input file");
        }

        return Created();
    }

    [HttpPost("create-fragment")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CreateAudioFragment([FromForm] AudioEditDto audioTrackDto)
    {
        if (string.IsNullOrWhiteSpace(audioTrackDto.Title)
            || audioTrackDto.StartsAt.Equals(audioTrackDto.EndsAt)
            || audioTrackDto.StartsAt > audioTrackDto.EndsAt
            || audioTrackDto.OperationId == Guid.Empty)
        {
            return BadRequest("Incorrect data");
        }

        if (audioTrackDto.Title.Length > _maxTitleLength || !LettersNumbersSymbolsRegex().IsMatch(audioTrackDto.Title))
            return BadRequest("Incorrect format of Title");

        var state = _dbContext.AudioExtractionState
            .AsNoTracking()
            .FirstOrDefault(s => s.RequestId == audioTrackDto.OperationId);

        if (state == null)
            return Problem("Operation with this id was not found", HttpContext.Request.Path, StatusCodes.Status404NotFound);

        if (!string.Equals(state.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            return Problem("Operation is not marked as succeeded", HttpContext.Request.Path, StatusCodes.Status400BadRequest);

        var audioInputPath = Path.Combine(_audioExtractionDirectory, $"{state.Name}.mp3");

        if (!System.IO.File.Exists(audioInputPath))
            return Problem("Input file was not found", HttpContext.Request.Path, StatusCodes.Status404NotFound);

        string fileOutputPath = Path.Combine(_audioExtractionDirectory, $"{audioTrackDto.Title}.mp3");
        var start = TimeSpan.FromMilliseconds(audioTrackDto.StartsAt.TotalMilliseconds);
        TimeSpan? end = audioTrackDto.EndsAt.IsTillTheEnd
        ? null
        : TimeSpan.FromMilliseconds(audioTrackDto.EndsAt.TotalMilliseconds);

        try
        {
            var res = await _ytAudioExtractor.CutAndSaveAudioAsync(audioInputPath, fileOutputPath, start, end);
            if (res is Error error)
            {
                Log.Error("Audio extraction failed with message: {0}", error.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, error.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Audio extraction failed with message: {0}", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, "Audio processing failed");
        }

        if (!System.IO.File.Exists(fileOutputPath))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to save file");
        }

        try
        {
            System.IO.File.Delete(audioInputPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while trying to delete input file");
        }

        var stream = System.IO.File.OpenRead(fileOutputPath);
        return File(stream, "audio/mpeg", fileDownloadName: $"{state.Name}.mp3", enableRangeProcessing: true);
    }




    private async Task RunExtractionAsync(string directoryPath, Guid requestId, BeginAudioExtractionDto dto)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var extractor = scope.ServiceProvider.GetRequiredService<YtAudioExtractorService>();

        var state = db.AudioExtractionState
            .FirstOrDefault(s => s.RequestId == requestId);

        if (state is null)
        {
            Log.Error("State row for {0} not found", requestId);
            return;
        }

        var rndName = Path.GetRandomFileName().Remove(8, 1);
        state.Name = rndName;

        try
        {
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            var res = await extractor.DownloadAudioFragmentAsync(
                dto.Url, dto.StartsAt, dto.EndsAt, rndName, directoryPath);

            if (res.Result.Success)
            {
                state.Status = "succeeded";
                state.ResultMessage = "Successful Work";
            }
            else
            {
                state.Status = "failed";
                state.ResultMessage = string.Concat(res.Result.ErrorOutput);
                Log.Error("Failed to extract yt audio on record with {0} id", state.Key);
            }
        }
        catch (Exception ex)
        {
            state.Status = "failed";
            state.ResultMessage = ex.Message;
            Log.Error(ex, "Extraction {0} failed", state.Key);
        }
        finally
        {
            db.SaveChanges();
        }
    }

    private static string GetCheckStatusUrl(string basePath, string endpointPath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            Log.Warning("Base route was empty");
            return string.Empty;
        }

        if (!basePath.EndsWith('/'))
            basePath += "/";

        endpointPath = endpointPath.TrimStart('/');
        Uri baseUri = new(basePath);
        return new Uri(baseUri, endpointPath).ToString();
    }

    [GeneratedRegex(@"^[\p{L}\p{N}_*. ,:&?!@#$%()<>-]+$")]
    private static partial Regex LettersNumbersSymbolsRegex();
}
