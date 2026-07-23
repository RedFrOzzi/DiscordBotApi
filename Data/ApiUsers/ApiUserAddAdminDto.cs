using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.ApiUsers;

public class ApiUserAddAdminDto
{
    [Required] public string Login { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    [Required] public string Keyword { get; set; } = string.Empty;
    [Required] public string NewAdminLogin { get; set; } = string.Empty;
}
