using DiscordBotApi.Utilities;
using Microsoft.Extensions.Options;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace DiscordBotApi.Services;

public class YtAudioExtractorService
{
    private readonly YoutubeDL _ytdl;
    readonly string? _denoPath;
    readonly Option<bool> _forceKeyframesOption;

    public YtAudioExtractorService()
    {
        _ytdl = new();

        var ffmpegPath = Environment.GetEnvironmentVariable("FFMPEG_FILE_PATH");
        if (!string.IsNullOrWhiteSpace(ffmpegPath))
        {
            _ytdl.FFmpegPath = ffmpegPath;
        }

        var _ytdlpPath = Environment.GetEnvironmentVariable("YTDLP_FILE_PATH");
        if (!string.IsNullOrWhiteSpace(_ytdlpPath))
        {
            _ytdl.YoutubeDLPath = _ytdlpPath;
        }

        _denoPath = Environment.GetEnvironmentVariable("DENO_FILE_PATH");

        _forceKeyframesOption = new Option<bool>(true, "--force-keyframes-at-cuts")
        {
            Value = true
        };
    }

    public async Task<RunResult<string>> DownloadAudioFragmentAsync(string url, TimeStamp start, TimeStamp end, string outputFolderPath, string outputName)
    {
        var path = Path.Combine(outputFolderPath, $"{outputName}.%(ext)s");
        var options = new OptionSet();

        if (_denoPath != null)
        {
            options.AddCustomOption<string>("--js-runtimes", $"deno:{_denoPath}");
        }

        options.ExtractAudio = true;
        options.AudioFormat = AudioConversionFormat.Mp3;
        options.Output = path;

        options.AddCustomOption<string>("--download-sections", $"*{start}-{end}");
        options.CustomOptions = [.. options.CustomOptions, _forceKeyframesOption];

        var res = await _ytdl.RunWithOptions(url, options);

        return res;
    }
}
