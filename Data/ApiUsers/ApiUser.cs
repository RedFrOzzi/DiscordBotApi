using DiscordBotApi.Data.DiscordUsers;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.ApiUsers;

public class ApiUser
{
    [Key] public int Key { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string Login { get; set; } = string.Empty;
    [Required] public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; } = false;
    public DiscordUser? DiscordUser { get; set; }
}
