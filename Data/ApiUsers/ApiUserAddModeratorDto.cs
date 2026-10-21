namespace DiscordBotApi.Data.ApiUsers;

public class ApiUserAddModeratorDto
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string NewModeratorLogin { get; set; } = string.Empty;
}
