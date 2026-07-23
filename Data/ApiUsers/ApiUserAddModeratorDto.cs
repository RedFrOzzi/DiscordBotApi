using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.ApiUsers;

public class ApiUserAddModeratorDto
{
    [Required] public string Login { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    [Required] public string NewModeratorLogin { get; set; } = string.Empty;
}