using System.Collections.Concurrent;

namespace DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;

public class VoiceInstancesContainer
{
    public ConcurrentDictionary<ulong, VoiceInstance?> VoiceInstances { get => _voiceInstances; set => _voiceInstances = value; }

    private static ConcurrentDictionary<ulong, VoiceInstance?> _voiceInstances = [];
}
