namespace DiscordBotApi.Data.AudioTracks;

public class AudioTrackUploadDto
{
    public string? Title { get; set; }
    public IFormFile? File { get; set; }
    public string? GuildId { get; set; }
}
