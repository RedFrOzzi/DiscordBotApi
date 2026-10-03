namespace DiscordBotApi.Data.ApiUsers;

public class ApiUserLinkDto
{
    public Guid? ApiUserId { get; set; }
    public ulong? DiscordUserId { get; set; }
}
