using DiscordBotApi.Data.VoiceStates;
using NetCord.Gateway;

namespace DiscordBotApi.Data.DiscordUsers.Dtos
{
    public class DiscordUserGetDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Nickname { get; set; } = string.Empty;
        public string? GlobalName { get; set; } = string.Empty;
        public string? ImageURL { get; set; } = string.Empty;
        public VoiceStateDto? VoiceState { get; set; }
        public int UserResource { get; set; }

        public void AddVoiceState(VoiceState voiceState)
        {
            if (voiceState == null)
            {
                VoiceState = null;
                return;
            }

            string channelId;
            if (voiceState.ChannelId == null)
                channelId = "";
            else
                channelId = voiceState.ChannelId.ToString()!;

            VoiceState = new()
            {
                ChannelId = channelId,
                IsMuted = voiceState.IsMuted || voiceState.IsSelfMuted,
                IsDeafened = voiceState.IsDeafened || voiceState.IsSelfDeafened,
            };
        }
    }
}
