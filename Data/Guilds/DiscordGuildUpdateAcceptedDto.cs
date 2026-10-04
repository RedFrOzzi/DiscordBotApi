namespace DiscordBotApi.Data.Guilds;

public class DiscordGuildUpdateAcceptedDto
{
    public string JobId { get; set; } = string.Empty;
    public string GuildId { get; set; } = string.Empty;
    public DateTimeOffset EnqueuedAt { get; set; } = DateTime.MinValue;
}
