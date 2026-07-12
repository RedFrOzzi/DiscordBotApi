using DiscordBotApi.Data.Guilds;
using DiscordBotApi.Data.Roles;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.Raffles;

public class RaffleSettings
{
    [Key] public int Id { get; set; }
    [Required] public DiscordGuild Guild { get; set; } = null!;
    public ICollection<DiscordGuildRole> AllowedRoles { get; set; } = [];
}
