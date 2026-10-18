namespace DiscordBotApi.Data.TelegramBotImages;

public class TelegramBotImageGetDto
{
    public int? Key { get; set; }
    public string? Command { get; set; } = string.Empty;
    public string? ImageFileName { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; } = string.Empty;
}
