namespace DiscordBotApi.Data.AudioTracks;

public class AudioTrackDto
{
    public int Key { get; set; }
    public string? Title { get; set; }
    public string? GuildId { get; set; }
    public double? SizeInKb { get; set; }
    public string? CreatedAt { get; set; }
    public string? DownloadUrl { get; set; }
}
