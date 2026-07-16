using DiscordBotApi.Data.DiscordUsers;
using DiscordBotApi.Data.Guilds;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.Roles;

public class DiscordGuildRole
{
    [Key] public int Key { get; set; }
    [Required] public ulong Id { get; set; }
    [Required] public DiscordGuild DiscordGuild { get; set; } = null!;
    public string? Name { get; set; }
    public ICollection<DiscordUser> DiscordUsersWithRole { get; set; } = [];
}
