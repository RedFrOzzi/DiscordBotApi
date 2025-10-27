using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.Users
{
    public class DiscordUser
    {
        [Key]
        public int PrimaryKey { get; set; }
        [Required]
        public ulong Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Nickname { get; set; } = string.Empty;
        public string? GlobalName { get; set; } = string.Empty;
        public string? ImageURL { get; set; } = string.Empty;
    }
}
