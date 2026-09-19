using DiscordBotApi.Utilities;

namespace DiscordBotApi.Data.YtAudioExtractorData;

public class YtAudioExtractorData
{
    public ulong GuildId { get; set; }
    public string URL { get; set; } = string.Empty;
    public string OutputName { get; set; } = string.Empty;
    public TimeStamp StartsAt { get; set; }
    public TimeStamp EndsAt { get; set; }
}
