using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.AudioPanels;

public class AudioPanel
{
    [Key] public int Key { get; set; }
    [Required] public ulong GuildId { get; set; }
    [Required] public ulong ChannelId { get; set; }
    [Required] public ulong[] MessageIds { get; set; } = [];
}
