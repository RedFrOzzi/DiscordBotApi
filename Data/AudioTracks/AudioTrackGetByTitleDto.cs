namespace DiscordBotApi.Data.AudioTracks;

public class AudioTrackGetByTitleDto
{
    public ulong GuildId { get; set; }
    public string? Title { get; set; }
}
