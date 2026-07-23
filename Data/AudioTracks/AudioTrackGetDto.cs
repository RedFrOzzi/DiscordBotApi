namespace DiscordBotApi.Data.AudioTracks;

public class AudioTrackGetDto
{
    public string? Title { get; set; }
    public string? GuildId { get; set; }
    public double? SizeInKb { get; set; }
    public string? CreatedAt { get; set; }
}
