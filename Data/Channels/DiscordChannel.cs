using DiscordBotApi.Data.Guilds;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.Channels
{
    public class DiscordChannel
    {
        [Key] public int Key { get; set; }
        [Required] public ulong Id { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public bool IsTextChannel { get; set; }
        [Required] public DiscordGuild Guild { get; set; } = null!;
    }
}
