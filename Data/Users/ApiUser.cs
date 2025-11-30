using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.Users
{
    public class ApiUser
    {
        [Key]
        public int PrimaryKey { get; set; }
        public Guid ApiUserId { get; set; } = Guid.NewGuid();
        [Required]
        public string Login { get; set; } = string.Empty;
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsAdmin { get; set; } = false;
    }
}
