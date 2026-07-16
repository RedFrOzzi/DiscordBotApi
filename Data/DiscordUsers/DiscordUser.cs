using DiscordBotApi.Data.Guilds;
using DiscordBotApi.Data.Roles;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.DiscordUsers;

public class DiscordUser
{
    [Key] public int Key { get; set; }
    [Required] public ulong Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Nickname { get; set; } = string.Empty;
    public string? GlobalName { get; set; } = string.Empty;
    public string? ImageURL { get; set; } = string.Empty;
    public ICollection<DiscordGuild> Guilds { get; set; } = [];
    public ICollection<DiscordGuildRole> Roles { get; set; } = [];
    public int UserSpendingResource { get; set; }
}
