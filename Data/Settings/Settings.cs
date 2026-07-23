using DiscordBotApi.Data.Guilds;
using DiscordBotApi.Data.Roles;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.Settings;

public class Settings
{
    [Key] public int Key { get; set; }
    [Required] public DiscordGuild Guild { get; set; } = null!;
    public ICollection<DiscordGuildRole>? PrivilegedRoles { get; set; }
}
