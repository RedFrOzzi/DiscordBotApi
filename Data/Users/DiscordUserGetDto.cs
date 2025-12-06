using DiscordBotApi.Data.VoiceState;

namespace DiscordBotApi.Data.Users
{
    public class DiscordUserGetDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Nickname { get; set; } = string.Empty;
        public string? GlobalName { get; set; } = string.Empty;
        public string? ImageURL { get; set; } = string.Empty;
        public VoiceStateDto? VoiceState { get; set; }
        public int UserIQ { get; set; }

        public void AddVoiceState(NetCord.Gateway.VoiceState voiceState)
        {
            if (voiceState == null)
            {
                VoiceState = null;
                return;
            }

            VoiceState = new()
            {
                ChannelId = voiceState.ChannelId?.ToString(),
                IsMuted = voiceState.IsMuted || voiceState.IsSelfMuted,
                IsDeafened = voiceState.IsDeafened || voiceState.IsSelfDeafened,
            };
        }
    }
}
