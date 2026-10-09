using DiscordBotApi.Utilities;
using DiscordBotApi.Utilities.Result;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace DiscordBotApi.Services;

public class YtAudioExtractorService
{
    private readonly YoutubeDL _ytdl;
    readonly string? _denoPath;
    readonly string? _ffmpegPath;
    readonly string? _cookiesPath;
    readonly Option<bool> _forceKeyframesOption;

    public YtAudioExtractorService()
    {
        _ytdl = new();

        var value = Environment.GetEnvironmentVariable("FFMPEG_FILE_PATH");
        _ffmpegPath = string.IsNullOrWhiteSpace(value) ? "ffmpeg" : value;
        if (!string.IsNullOrWhiteSpace(value))
        {
            _ytdl.FFmpegPath = value;
        }

        var _ytdlpPath = Environment.GetEnvironmentVariable("YTDLP_FILE_PATH");
        if (!string.IsNullOrWhiteSpace(_ytdlpPath))
        {
            _ytdl.YoutubeDLPath = _ytdlpPath;
        }

        _denoPath = Environment.GetEnvironmentVariable("DENO_FILE_PATH");

        _cookiesPath = Path.Combine(AppContext.BaseDirectory, "cookies.txt");
        if (!File.Exists(_cookiesPath))
        {
            _cookiesPath = Environment.GetEnvironmentVariable("YT_COOKIES_FULL_PATH");
        }

        _forceKeyframesOption = new Option<bool>(true, "--force-keyframes-at-cuts")
        {
            Value = true
        };
    }

    public async Task<RunResult<string>> DownloadExactAudioFragmentAsync(string url, TimeStamp start, TimeStamp end, string outputFolderPath, string outputName)
    {
        var path = Path.Combine(outputFolderPath, $"{outputName}.%(ext)s");
        var options = new OptionSet();

        if (_denoPath != null)
        {
            options.AddCustomOption<string>("--js-runtimes", $"deno:{_denoPath}");
        }

        if (!string.IsNullOrWhiteSpace(_cookiesPath) && File.Exists(_cookiesPath))
        {
            options.AddCustomOption<string>("--cookies", _cookiesPath);
        }

        options.ExtractAudio = true;
        options.AudioFormat = AudioConversionFormat.Mp3;
        options.Output = path;

        options.AddCustomOption<string>("--download-sections", $"*{start}-{end}");
        options.CustomOptions = [.. options.CustomOptions, _forceKeyframesOption];

        var res = await _ytdl.RunWithOptions(url, options);

        return res;
    }

    public async Task<ExtractedAudioData> DownloadAudioFragmentAsync(string url, TimeStamp start, TimeStamp end, string fileName, string outputFolderPath)
    {
        var path = Path.Combine(outputFolderPath, $"{fileName}.%(ext)s");
        var options = new OptionSet();

        if (_denoPath != null)
        {
            options.AddCustomOption<string>("--js-runtimes", $"deno:{_denoPath}");
        }

        if (!string.IsNullOrWhiteSpace(_cookiesPath) && File.Exists(_cookiesPath))
        {
            options.AddCustomOption<string>("--cookies", _cookiesPath);
        }

        options.ExtractAudio = true;
        options.AudioFormat = AudioConversionFormat.Mp3;
        options.Output = path;

        options.AddCustomOption<string>("--download-sections", $"*{start}-{end}");

        var res = await _ytdl.RunWithOptions(url, options);

        return new()
        {
            Result = res,
            AudioName = fileName,
        };
    }

    public async Task<Result> CutAndSaveAudioAsync(
       string inputPath, string outputPath,
       TimeSpan start, TimeSpan? end,
       CancellationToken ct = default)
    {
        if (!File.Exists(inputPath))
        {
            return new Error("Input audio not found");
        }

        var dir = Path.GetDirectoryName(outputPath)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var args = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-i", inputPath,
            "-ss", start.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture),
        };

        if (end.HasValue)
        {
            if (end.Value <= start)
                return new Error("End must be after start");

            args.Add("-to");
            args.Add(end.Value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture));
        }

        args.Add("-map_metadata");
        args.Add("0");
        args.Add("-c:a");
        args.Add("libmp3lame");
        args.Add("-q:a");
        args.Add("2");
        args.Add(outputPath);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            Log.Error("ffmpeg failed ({0}): {1}", proc.ExitCode, stderr);
            return new Error($"ffmpeg exited with code {proc.ExitCode}");
        }

        return Success.Empty;
    }
}

public class ExtractedAudioData
{
    public RunResult<string> Result { get; init; } = null!;
    public string AudioName { get; init; } = string.Empty;
}