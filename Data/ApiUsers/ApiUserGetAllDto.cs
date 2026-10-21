namespace DiscordBotApi.Data.ApiUsers;

public class ApiUserGetAllDto
{
    public string Id { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public bool IsAdmin { get; set; } = false;
    public bool IsModerator { get; set; } = false;
    public bool IsTelegramUser { get; set; } = false;
    public ulong? DiscordUserId { get; set; }
}
