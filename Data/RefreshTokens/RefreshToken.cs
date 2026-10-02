using DiscordBotApi.Data.ApiUsers;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.RefreshTokens;

public class RefreshToken
{
    [Key] public int Key { get; set; }
    [Required] public ApiUser User { get; set; } = null!;
    [Required] public string TokenHash { get; set; } = null!;
    [Required] public DateTime ExpiresAt { get; set; }
    [Required] public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
