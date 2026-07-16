using DiscordBotApi.Data.Channels;
using DiscordBotApi.Data.DiscordUsers;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.Guilds;

public class DiscordGuild
{
    [Key] public int Key { get; set; }
    [Required] public ulong Id { get; set; }
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public DiscordUser Owner { get; set; } = null!;
    public ICollection<DiscordUser> Users { get; set; } = [];
    public ICollection<DiscordChannel> Channels { get; set; } = [];
}
