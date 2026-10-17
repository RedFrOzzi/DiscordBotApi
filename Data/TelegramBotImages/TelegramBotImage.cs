using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.TelegramBotImages;

public class TelegramBotImage
{
    [Key] public int Key { get; set; }
    [Required] public string Command { get; set; } = string.Empty;
    [Required] public string ImageFileName { get; set; } = string.Empty;
}
