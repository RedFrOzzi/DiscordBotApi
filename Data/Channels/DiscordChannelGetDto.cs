using DiscordBotApi.Data.Guilds;

namespace DiscordBotApi.Data.Channels
{
    public class DiscordChannelGetDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; } = string.Empty;
        public bool? IsTextChannel { get; set; }
        public string? GuildId { get; set; }
    }
}
