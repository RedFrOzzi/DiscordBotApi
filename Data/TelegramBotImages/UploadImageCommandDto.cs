using DiscordBotApi.TelegramBot;

namespace DiscordBotApi.Data.TelegramBotImages;

public class UploadImageCommandDto
{
    public string? Command { get; set; }
    public IFormFile? Image { get; set; }
    public CropPosition? CropPosition { get; set; }
}
