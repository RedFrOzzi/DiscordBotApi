using DiscordBotApi.Utilities;

namespace DiscordBotApi.Data.YtAudioExtractorDatas;

public class AudioEditDto
{
    public Guid OperationId { get; set; }
    public ulong GuildId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TimeStamp StartsAt { get; set; }
    public TimeStamp EndsAt { get; set; }
}
