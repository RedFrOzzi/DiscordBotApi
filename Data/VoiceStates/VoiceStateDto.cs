namespace DiscordBotApi.Data.VoiceStates
{
    public class VoiceStateDto
    {
        public string ChannelId { get; set; } = string.Empty;
        public bool IsMuted { get; set; }
        public bool IsDeafened { get; set; }
    }
}
