namespace DiscordBotApi.Data.DiscordUsers;

public class DiscordUserGetVoiceStateDto
{
    public string? UserId { get; set; }
    public string? ChannelId { get; set; }
    public bool? IsDeafened { get; set; }
    public bool? IsMuted { get; set; }
}