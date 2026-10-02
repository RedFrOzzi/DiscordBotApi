using DiscordBotApi.Utilities;

namespace DiscordBotApi.Data.YtAudioExtractorDatas;
public class BeginAudioExtractionDto
{
    public string Url { get; set; } = string.Empty;
    public TimeStamp? StartsAt { get; set; }
    public TimeStamp? EndsAt { get; set; }
}