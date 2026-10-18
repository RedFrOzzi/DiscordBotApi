using DiscordBotApi.Data.AudioTracks;
using DiscordBotApi.Data.TelegramBotImages;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.TelegramBot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("tg-bot")]
public class TelegramBotController(TelegramBotService botService, ApplicationDbContext dbContext) : ControllerBase
{
    readonly TelegramBotService _botService = botService;
    readonly ApplicationDbContext _dbContext = dbContext;
    readonly string _tgSecret = Environment.GetEnvironmentVariable("TELEGRAM_BOT_SECRET") ?? string.Empty;
    readonly static string _baseImagePath = Path.Combine(AppContext.BaseDirectory, "ImageStorage");
    const long _maxSize = 10485760;

    [HttpPost("update")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ReciveTelegramUpdate(CancellationToken cancellationToken)
    {
        var receivedSecret = HttpContext.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(receivedSecret) || !receivedSecret.Equals(_tgSecret))
        {
            return Unauthorized();
        }

        Update? update;
        try
        {
            update = await JsonSerializer.DeserializeAsync<Update>(
                Request.Body,
                JsonBotAPI.Options,
                cancellationToken);
        }
        catch (JsonException jex)
        {
            Log.Error(jex, "Erron on deserialization update json from telegram");
            return BadRequest();
        }

        if (update is not null)
            await _botService.HandleUpdateAsync(update, cancellationToken);

        return Ok();
    }

    [HttpPost("image-upload")]
    [Authorize(Roles = "Admin, Moderator")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UploadImage([FromForm] UploadImageCommandDto uploadDto, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(uploadDto.Command))
            return BadRequest("Missing Command Name");

        if (!TelegramBotHelpers.TryNormalizeWord(uploadDto.Command, out var normalized))
            return BadRequest("Word must be a single word");

        if (uploadDto.Image is null || uploadDto.Image.Length == 0)
            return BadRequest(new { error = "Image is required" });

        if (uploadDto.Image.Length > _maxSize)
            return BadRequest(new { error = $"Image too large (max {_maxSize} bytes)." });

        byte[] bytes;
        await using (var src = uploadDto.Image.OpenReadStream())
        {
            using var ms = new MemoryStream(capacity: (int)uploadDto.Image.Length);
            await src.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        var format = TelegramBotHelpers.Detect(bytes);
        if (format is null)
            return BadRequest(new { error = "Unsupported image format. Allowed: JPEG, PNG, GIF, WebP." });

        var fileName = normalized + format.Extension;
        var fullPath = Path.Combine(_baseImagePath, fileName);

        var existing = _dbContext.TelegramBotImages.FirstOrDefault(w => w.Command == normalized);
        var oldFile = existing?.ImageFileName;

        var tmpPath = fullPath + ".tmp";
        try
        {
            await using (var fs = System.IO.File.Create(tmpPath))
            {
                await fs.WriteAsync(bytes, ct);
            }

            System.IO.File.Move(tmpPath, fullPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tmpPath);
            throw;
        }

        if (existing == null)
        {
            TelegramBotImage newImage = new()
            {
                Command = normalized,
                ImageFileName = fileName
            };

            _dbContext.TelegramBotImages.Add(newImage);
        }
        else
        {
            existing.ImageFileName = fileName;
        }

        _dbContext.SaveChanges();

        if (oldFile is not null && oldFile != fileName)
            TryDeleteFile(Path.Combine(_baseImagePath, oldFile));

        return Ok();
    }

    [HttpDelete("{key:int}/image-delete")]
    [Authorize(Roles = "Admin, Moderator")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int key)
    {
        if (key < 0)
            return BadRequest("Invalid Key");

        var existing = _dbContext.TelegramBotImages.FirstOrDefault(r => r.Key == key);
        if (existing is null)
            return NotFound();

        _dbContext.TelegramBotImages.Remove(existing);
        _dbContext.SaveChanges();

        TryDeleteFile(Path.Combine(_baseImagePath, existing.ImageFileName));

        return NoContent();
    }

    [HttpGet("{key:int}/image-download")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadImagerecord(int key)
    {
        var record = _dbContext.TelegramBotImages
            .AsNoTracking()
            .Where(r => r.Key == key)
            .Select(r => new
            {
                r.ImageFileName
            })
            .FirstOrDefault();

        if (record == null)
            return NotFound();

        var filePath = Path.Combine(_baseImagePath, record.ImageFileName!);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(record.ImageFileName!, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var stream = System.IO.File.OpenRead(filePath);
        return File(stream, contentType, fileDownloadName: record.ImageFileName!, enableRangeProcessing: false);
    }

    [HttpGet("image-get-all")]
    [Authorize(Roles = "Admin, Moderator")]
    [ProducesResponseType<IEnumerable<TelegramBotImageGetDto>>(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetImages()
    {
        var records = _dbContext.TelegramBotImages
            .AsNoTracking()
            .Select(r => new TelegramBotImageGetDto()
            {
                Key = r.Key,
                Command = r.Command,
                ImageFileName = r.ImageFileName,
                DownloadUrl = $"/tg-bot/{r.Key}/image-download",
            })
            .AsEnumerable();

        if (records is null)
            return NotFound();

        return Ok(records);
    }

    [HttpPatch("image-rename")]
    [Authorize(Roles = "Admin, Moderator")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RenameCommand([FromBody] TelegramBotImageRenameDto dto)
    {
        if (dto is null
            || dto.Key < 0
            || string.IsNullOrWhiteSpace(dto.NewCommand))
            return BadRequest();

        var record = _dbContext.TelegramBotImages
            .FirstOrDefault(r => r.Key == dto.Key);

        if (record is null)
            return NotFound();

        record.Command = dto.NewCommand;
        _dbContext.SaveChanges();

        return Ok();
    }


    private static void TryDeleteFile(string path)
    {
        try
        {
            if (Path.GetFileName(path) != Path.GetFileName(Path.Combine(_baseImagePath, Path.GetFileName(path))))
                return;

            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Не удалось удалить файл {Path}", path);
        }
    }
}
