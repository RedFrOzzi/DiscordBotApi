using DiscordBotApi.Data.AudioTracks;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;
using DiscordBotApi.Utilities.Result;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;
using NetCord.Rest;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DiscordBotApi.DiscordBot.Services;

public partial class AudioFilesService()
{
    readonly static string _basePath = Path.Combine(AppContext.BaseDirectory, "DataStorage");
    const long _reservedFreeSpace = 536900000; //0.5 gb
    const int _maxTitleLength = 79;
    static readonly ConcurrentDictionary<string, string> _titleToPathCollection = [];

    public static async Task<Result> TrySaveFile(ApplicationDbContext dbContext, IFormFile file, string title, ulong guildId)
    {
        if (guildId <= 0)
            return new BadRequesError("Не верно указан канал");

        if (file == null || file.Length == 0)
            return new BadRequesError("Пустой файл");

        if (title.Length > _maxTitleLength || !LettersNumbersSymbolsRegex().IsMatch(title))
            return new BadRequesError("Ошибка названия трека");

        if (!file.FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) 
            && file.ContentType != "audio/mpeg")
        {
            return new BadRequesError("Поддерживаются только файлы mp3");
        }

        var freeSpace = new DriveInfo(AppContext.BaseDirectory).AvailableFreeSpace;

        if (freeSpace < _reservedFreeSpace)
            return new BadRequesError("Не достаточно место на диске");

        var directory = Path.Combine(_basePath, guildId.ToString());

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        if (dbContext.AudioTracks.Any(t => t.Title == title))
            return new AlreadyExistError();

        var guild = dbContext.Guilds.FirstOrDefault(g => g.Id == guildId);
        if (guild == null)
            return new BadRequesError("Гильдия не найдена в базе данных");
        ;
        string filePath = Path.Combine(directory, title + ".mp3");
        TimeZoneInfo moscowZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
        DateTime moscowTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, moscowZone);
        var track =  new AudioTrack()
        {
            Title = title,
            Path = filePath,
            Guild = guild,
            CreatedAt = moscowTime,
        };
        track.SetSize(file.Length);

        try
        {
            using var fileStream = File.Create(filePath);
            await file.CopyToAsync(fileStream);
        }
        catch
        {
            return new InternalError("Внутренняя ошибка сервера");
        }

        dbContext.AudioTracks.Add(track);
        if (dbContext.SaveChanges() == 0)
            return new InternalError("Внутренняя ошибка сервера");

        return new Success<AudioTrack>("Успех", track);
    }

    public static async Task<Result> TrySaveFile(ApplicationDbContext dbContext, Stream downloadStream, long size, string title, ulong guildId)
    {
        if (guildId <= 0)
            return new BadRequesError("Не верно указан канал");

        if (downloadStream == null)
            return new BadRequesError("Пустой файл");

        if (title.Length > _maxTitleLength || !LettersNumbersSymbolsRegex().IsMatch(title))
            return new BadRequesError("Ошибка названия трека");

        var freeSpace = new DriveInfo(AppContext.BaseDirectory).AvailableFreeSpace;

        if (freeSpace < _reservedFreeSpace)
            return new BadRequesError("Не достаточно место на диске");

        var directory = Path.Combine(_basePath, guildId.ToString());

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        if (dbContext.AudioTracks.Any(t => t.Title == title))
            return new AlreadyExistError();

        var guild = dbContext.Guilds.FirstOrDefault(g => g.Id == guildId);
        if (guild == null)
            return new BadRequesError("Гильдия не найдена в базе данных");
        ;
        string filePath = Path.Combine(directory, title + ".mp3");
        TimeZoneInfo moscowZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
        DateTime moscowTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, moscowZone);
        var track = new AudioTrack()
        {
            Title = title,
            Path = filePath,
            Guild = guild,
            CreatedAt = moscowTime,
        };
        track.SetSize(size);

        try
        {
            using var fileStream = File.Create(filePath);
            await downloadStream.CopyToAsync(fileStream);
        }
        catch
        {
            return new InternalError("Внутренняя ошибка сервера");
        }

        dbContext.AudioTracks.Add(track);
        if (dbContext.SaveChanges() == 0)
            return new InternalError("Внутренняя ошибка сервера");

        return new Success<AudioTrack>("Успех", track);
    }

    public static Result DeleteFile(ApplicationDbContext dbContext, string title)
    {
        if (string.IsNullOrEmpty(title))
            return new BadRequesError("Неверное название трека");

        var audioTrack = dbContext.AudioTracks
            .FirstOrDefault(t => t.Title == title);

        if (audioTrack == null)
            return new NotFoundError("Файл не найден");

        try
        {
            File.Delete(audioTrack.Path);
        }
        catch
        {
            return new InternalError("Внутреняя ошибка сервера");
        }

        dbContext.AudioTracks.Remove(audioTrack);
        if (dbContext.SaveChanges() == 0)
            return new InternalError("Внутреняя ошибка сервера");

        return Success.Empty;
    }

    public static string? GetAudioTrackPath(ApplicationDbContext dbContext, string title)
    {
        if (_titleToPathCollection.TryGetValue(title, out var path))
            return path;

        var audioTrack = dbContext.AudioTracks
            .AsNoTracking()
            .FirstOrDefault(t => t.Title == title);

        if (audioTrack == null)
            return null;

        _titleToPathCollection.TryAdd(title, audioTrack.Path);
        return audioTrack.Path;
    }

    [GeneratedRegex(@"^[\p{L}\p{N}_*. ,:&?!@#$%()<>-]+$")]
    private static partial Regex LettersNumbersSymbolsRegex();
}
