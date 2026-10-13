using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBotApi.Utilities;

public partial class LogsParser
{
    public sealed partial class LogFileReader(string path)
    {
        private static readonly Regex _rx = RegexParser();

        public IEnumerable<LogEntry> ReadBackwards()
        {
            using var fs = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs, Encoding.UTF8);

            var text = reader.ReadToEnd();
            var lines = text.Split('\n');

            LogEntry? current = null;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].TrimEnd('\r');
                var m = _rx.Match(line);

                if (!m.Success)
                {
                    if (current is not null && !string.IsNullOrWhiteSpace(line))
                        current = current with { Message = line + "\n" + current.Message };
                    continue;
                }

                if (current is not null) yield return current;

                var ts = DateTime.ParseExact(m.Groups["ts"].Value,
                    "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                current = new LogEntry(ts, m.Groups["lvl"].Value, m.Groups["msg"].Value);
            }
            if (current is not null) yield return current;
        }

        [GeneratedRegex(@"^\[(?<ts>\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}) (?<lvl>\w+)\] (?<msg>.*)$", RegexOptions.Compiled)]
        private static partial Regex RegexParser();
    }

    public sealed record LogEntry(DateTime Ts, string Level, string Message);

    public sealed record LogPage(IReadOnlyList<LogEntry> Items, string? NextCursor, bool HasMore);

    public static class Cursor
    {
        public static string? Encode(int offset) =>
            offset == 0 ? null : Convert.ToBase64String(BitConverter.GetBytes(offset));
        public static int Decode(string? s) =>
            string.IsNullOrEmpty(s) ? 0 : BitConverter.ToInt32(Convert.FromBase64String(s));
    }
}