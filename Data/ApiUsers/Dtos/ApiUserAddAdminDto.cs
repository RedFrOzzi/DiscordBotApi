using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.ApiUsers.Dtos;

public class ApiUserAddAdminDto
{
    [Required] public string UserLogin { get; set; } = string.Empty;
    [Required] public string Keyword { get; set; } = string.Empty;
}
