using ImageMagick;
using Serilog;
using System.Diagnostics;

namespace DiscordBotApi.TelegramBot;

public class TelegramBotImageCropService
{
    static readonly string _ffmpeg = Environment.GetEnvironmentVariable("FFMPEG_FILE_PATH") ?? "ffmpeg";
    const int _targetWidth = 150;
    const int _targetHeight = 150;

    public static byte[] Crop(byte[] bytes, CropPosition position)
    {
        var format = DetectFormat(bytes);

        try
        {
            return IsAnimated(bytes, format)
                ? CropAnimation(bytes, _targetWidth, _targetHeight, position, format)
                : CropStatic(bytes, _targetWidth, _targetHeight, position, format);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка кропа изображения (target={W}x{H}, pos={Pos})", _targetWidth, _targetHeight, position);
            throw;
        }
    }

    private static byte[] CropStatic(
        byte[] bytes, int targetWidth, int targetHeight,
        CropPosition position, MagickFormat format)
    {
        using var image = new MagickImage(bytes);
        image.AutoOrient();

        if (image.Height > targetHeight)
            image.Resize(new MagickGeometry($"x{targetHeight}"));

        int newW = (int)image.Width;
        int newH = (int)image.Height;

        if (newW <= targetWidth)
        {
            image.ResetPage();
            image.Quality = 90;
            return image.ToByteArray(format);
        }

        int x = position switch
        {
            CropPosition.Left => 0,
            CropPosition.Right => newW - targetWidth,
            _ => (newW - targetWidth) / 2
        };

        image.Crop(new MagickGeometry(x, 0, (uint)targetWidth, (uint)newH));
        image.ResetPage();
        image.Quality = 90;
        return image.ToByteArray(format);
    }

    private static byte[] CropAnimation(
        byte[] bytes, int targetWidth, int targetHeight,
        CropPosition position, MagickFormat format)
    {
        using var collection = new MagickImageCollection(bytes);
        if (collection.Count == 0)
            return bytes;

        collection.Coalesce();
        var first = collection[0];

        if (first.Height > targetHeight)
        {
            var resizeGeo = new MagickGeometry($"x{targetHeight}");
            foreach (var frame in collection)
                frame.Resize(resizeGeo);
        }

        first = collection[0];
        int newW = (int)first.Width;
        int newH = (int)first.Height;

        if (newW <= targetWidth)
        {
            foreach (var frame in collection)
                frame.ResetPage();
            return collection.ToByteArray(format);
        }

        int x = position switch
        {
            CropPosition.Left => 0,
            CropPosition.Right => newW - targetWidth,
            _ => (newW - targetWidth) / 2
        };

        var geo = new MagickGeometry(x, 0, (uint)targetWidth, (uint)newH);
        foreach (var frame in collection)
        {
            frame.Crop(geo);
            frame.ResetPage();
        }

        return collection.ToByteArray(format);
    }

    public static byte[] ConvertAnimatedWebPToGif(byte[] webpBytes)
    {
        using var collection = new MagickImageCollection(webpBytes);
        if (collection.Count == 0)
            throw new InvalidOperationException("WebP не содержит кадров");

        collection.Coalesce();
        var quantizeSettings = new QuantizeSettings
        {
            Colors = 256,
            DitherMethod = DitherMethod.FloydSteinberg
        };
        collection.Quantize(quantizeSettings);
        return collection.ToByteArray(MagickFormat.Gif);
    }

    public static (byte[] Bytes, TelegramBotHelpers.ImageFormat Format) ConvertStaticWeb(byte[] webpBytes, uint quality = 90)
    {
        using var image = new MagickImage(webpBytes);
        if (image.HasAlpha)
        {
            image.Format = MagickFormat.Png;
            image.Strip();
            return (image.ToByteArray(), new(".png", "image/png"));
        }

        image.Format = MagickFormat.Jpeg;
        image.Quality = quality;
        image.Strip();
        return (image.ToByteArray(), new(".jpg", "image/jpeg"));
    }

    public static byte[] ConvertMp4ToGif(byte[] mp4Bytes, int fps = 10, int? maxWidth = null)
    {
        var readSettings = new MagickReadSettings
        {
            Format = MagickFormat.Mp4,
            FrameCount = 150
        };

        readSettings.SetDefine(MagickFormat.Mp4, "fps", fps.ToString());
        using var rawFrames = new MagickImageCollection(mp4Bytes, readSettings);
        if (rawFrames.Count == 0)
            throw new InvalidOperationException("Видео не содержит кадров.");

        using var fullFrames = new MagickImageCollection();
        var firstFrame = rawFrames[0].Clone();
        firstFrame.ResetPage();
        fullFrames.Add(firstFrame);
        var previousFullFrame = firstFrame;

        for (int i = 1; i < rawFrames.Count; i++)
        {
            var currentDeltaFrame = rawFrames[i];
            previousFullFrame.Composite(currentDeltaFrame, currentDeltaFrame.Page.X, currentDeltaFrame.Page.Y, CompositeOperator.Over);
            var completeFrame = previousFullFrame.Clone();
            completeFrame.ResetPage();
            fullFrames.Add(completeFrame);
        }

        if (maxWidth is > 0 and int targetWidth)
        {
            var resizeGeo = new MagickGeometry($"x{targetWidth}");
            foreach (var frame in fullFrames)
            {
                frame.Resize(resizeGeo);
            }
        }

        var quantizeSettings = new QuantizeSettings
        {
            Colors = 256,
            DitherMethod = DitherMethod.FloydSteinberg
        };

        fullFrames.Quantize(quantizeSettings);
        fullFrames.Optimize();

        return fullFrames.ToByteArray(MagickFormat.Gif);
    }

    public static async Task<byte[]> ConvertMp4ToGif2(
        byte[] mp4Bytes,
        int fps = 10,
        int? maxWidth = null,
        double startTimeSeconds = 0,
        double? endTimeSeconds = null,
        CancellationToken cancellationToken = default)
    {
        string durationArg = "";
        if (endTimeSeconds.HasValue)
        {
            var duration = endTimeSeconds.Value - startTimeSeconds;
            if (duration <= 0)
                throw new ArgumentException("End time must be greater than start time.");

            durationArg = $"-t {duration.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        var ffmpegArgs = $"-i pipe:0 " +
                 $"-ss {startTimeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                 (endTimeSeconds.HasValue
                     ? $"-to {endTimeSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)} "
                     : "") +
                 $"-vf fps={fps} " +
                 $"-f image2pipe -vcodec png pipe:1";

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpeg,
            Arguments = ffmpegArgs,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var writeTask = Task.Run(async () =>
        {
            await using var stdin = process.StandardInput.BaseStream;
            await stdin.WriteAsync(mp4Bytes);
        }, cancellationToken);

        using var ms = new MemoryStream();
        var readTask = process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(writeTask, readTask, process.WaitForExitAsync(cancellationToken));

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException($"FFmpeg failed: {error}");
        }

        ms.Position = 0;
        using var collection = new MagickImageCollection(ms.ToArray());

        if (collection.Count == 0)
            throw new InvalidOperationException("FFmpeg не извлёк ни одного кадра.");

        if (maxWidth is > 0 and int targetWidth)
        {
            var resizeGeo = new MagickGeometry($"x{targetWidth}");
            foreach (var frame in collection)
                frame.Resize(resizeGeo);
        }

        var quantizeSettings = new QuantizeSettings
        {
            Colors = 256,
            DitherMethod = DitherMethod.FloydSteinberg
        };
        collection.Quantize(quantizeSettings);
        collection.Optimize();

        return collection.ToByteArray(MagickFormat.Gif);
    }

    public static bool IsAnimatedWebP(byte[] bytes)
    {
        for (int i = 12; i < bytes.Length - 4; i++)
        {
            if (bytes[i] == 0x41 && bytes[i + 1] == 0x4E &&
                bytes[i + 2] == 0x49 && bytes[i + 3] == 0x4D)
                return true;
        }
        return false;
    }

    public static TelegramBotHelpers.ImageFormat? Detect(byte[] b)
    {
        if (b.Length >= 8 && b[4] == 0x66 && b[5] == 0x74 && b[6] == 0x79 && b[7] == 0x70)
            return new(".mp4", "video/mp4");

        return null;
    }

    private static MagickFormat DetectFormat(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return MagickFormat.Jpeg;
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            return MagickFormat.Png;
        if (b.Length >= 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46)
            return MagickFormat.Gif;
        if (b.Length >= 12 &&
            b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 &&
            b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
            return MagickFormat.WebP;
        return MagickFormat.Jpeg;
    }

    private static bool IsAnimated(byte[] bytes, MagickFormat format)
    {
        if (format == MagickFormat.Gif)
            return true;

        if (format == MagickFormat.WebP)
        {
            for (int i = 12; i < bytes.Length - 4; i++)
            {
                if (bytes[i] == 0x41 && bytes[i + 1] == 0x4E &&
                    bytes[i + 2] == 0x49 && bytes[i + 3] == 0x4D)
                    return true;
            }
        }

        return false;
    }
}

public enum CropPosition
{ 
    Left, Center, Right
}