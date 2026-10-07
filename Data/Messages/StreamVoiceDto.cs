using edge_tts_net;

namespace DiscordBotApi.Data.Messages;

public class StreamVoiceDto
{
    public ulong GuildId { get; set; }
    public string Message { get; set; } = string.Empty;
    public TTSOption? Options { get; set; }
}
