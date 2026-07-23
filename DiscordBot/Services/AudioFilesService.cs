using DiscordBotApi.Data.AudioTracks;
using DiscordBotApi.Database;
using DiscordBotApi.Utilities.Result;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DiscordBotApi.DiscordBot.Services;

public partial class AudioFilesService()
{
    readonly static string _basePath = Path.Combine(AppContext.BaseDirectory, "DataStorage");
    const long _maxLength = 15 * 1024 * 1024; //Size in bytes
    static readonly ConcurrentDictionary<string, string> _titleToPathCollection = [];

    public static async Task<Result> TrySaveFile(ApplicationDbContext dbContext, IFormFile file, string title, ulong guildId)
    {
        if (file == null || file.Length == 0)
            return new BadRequesError("Empty file.");

        if (file.Length > _maxLength)
            return new BadRequesError("Large file.");

        if (title.Length > 12 || !LettersAndNumbersRegex().IsMatch(title))
            return new BadRequesError("Title should include only letters and numbers and be less then 10 characters.");

        if (!file.FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) 
            && file.ContentType != "audio/mpeg")
        {
            return new BadRequesError("Only mp3 files are accepted.");
        }

        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);

        if (dbContext.AudioTracks.Any(t => t.Title == title))
            return new AlreadyExistError();

        var guild = dbContext.Guilds.FirstOrDefault(g => g.Id == guildId);
        if (guild == null)
            return new BadRequesError("Wrong guild.");
        ;
        string filePath = Path.Combine(_basePath, title + ".mp3");
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
            return new InternalError("Internal Server Error.");
        }

        dbContext.AudioTracks.Add(track);
        if (dbContext.SaveChanges() == 0)
            return new InternalError("Internal Server Error.");

        return new Success<AudioTrack>("Success", track);
    }

    public static async Task<Result> TrySaveFile(ApplicationDbContext dbContext, Stream downloadStream, long size, string title, ulong guildId)
    {
        if (downloadStream == null)
            return new BadRequesError("Empty file.");

        if (title.Length > 12 || !LettersAndNumbersRegex().IsMatch(title))
            return new BadRequesError("Title should include only letters and numbers and be less then 10 characters.");

        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);

        if (dbContext.AudioTracks.Any(t => t.Title == title))
            return new AlreadyExistError();

        var guild = dbContext.Guilds.FirstOrDefault(g => g.Id == guildId);
        if (guild == null)
            return new BadRequesError("Wrong guild.");
        ;
        string filePath = Path.Combine(_basePath, title + ".mp3");
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
            return new InternalError("Internal Server Error.");
        }

        dbContext.AudioTracks.Add(track);
        if (dbContext.SaveChanges() == 0)
            return new InternalError("Internal Server Error.");

        return new Success<AudioTrack>("Success", track);
    }

    public static Result DeleteFile(ApplicationDbContext dbContext, AudioTrackDeleteDto audioTrackDto)
    {
        if (audioTrackDto == null || audioTrackDto.Title == null)
            return new BadRequesError("File info was not provided.");

        var audioTrack = dbContext.AudioTracks.FirstOrDefault(t => t.Title == audioTrackDto.Title);
        if (audioTrack == null)
            return new NotFoundError("Audio track was not found");

        dbContext.AudioTracks.Remove(audioTrack);
        if (dbContext.SaveChanges() == 0)
            return new InternalError("Internal Server Error.");

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

    [GeneratedRegex(@"^[a-zA-Z0-9]+$")]
    private static partial Regex LettersAndNumbersRegex();
}
