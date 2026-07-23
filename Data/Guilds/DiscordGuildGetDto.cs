namespace DiscordBotApi.Data.Guilds;

public class DiscordGuildGetDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? OwnerId { get; set; }
    public ICollection<string>? UserIds { get; set; }
    public ICollection<string>? ChannelIds { get; set; }
}
