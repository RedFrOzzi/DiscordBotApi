using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBotApi.TelegramBot;

public static partial class TelegramBotHelpers
{
    public static bool TryNormalizeWord(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        var composed = trimmed.Normalize(NormalizationForm.FormC);

        if (!LatinCharsRegex().IsMatch(composed))
            return false;

        normalized = composed.ToLowerInvariant();
        return true;
    }

    public static ImageFormat? Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return new(".jpg", "image/jpeg");

        if (b.Length >= 8 &&
            b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return new(".png", "image/png");

        if (b.Length >= 6 &&
            b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38)
            return new(".gif", "image/gif");

        if (b.Length >= 12 &&
            b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 &&
            b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
            return new(".webp", "image/webp");

        if (b.Length >= 12 &&
            b[4] == 0x66 && b[5] == 0x74 && b[6] == 0x79 && b[7] == 0x70)
        {
            ReadOnlySpan<byte> brand = b.Slice(8, 4);

            bool isVideoBrand =
                brand.SequenceEqual("isom"u8) ||   // ISO base media
                brand.SequenceEqual("iso2"u8) ||
                brand.SequenceEqual("iso4"u8) ||
                brand.SequenceEqual("iso5"u8) ||
                brand.SequenceEqual("iso6"u8) ||
                brand.SequenceEqual("mp41"u8) ||   // MP4 v1
                brand.SequenceEqual("mp42"u8) ||   // MP4 v2
                brand.SequenceEqual("avc1"u8) ||   // H.264 MP4
                brand.SequenceEqual("dash"u8) ||   // DASH segment
                brand.SequenceEqual("M4V "u8) ||   // Apple M4V
                brand.SequenceEqual("qt  "u8);     // QuickTime .mov

            if (isVideoBrand)
                return new(".mp4", "video/mp4");
        }

        return null;
    }

    public record ImageFormat(string Extension, string MimeType);

    [GeneratedRegex(@"^[A-Za-zА-Яа-яЁё0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex LatinCharsRegex();
}
