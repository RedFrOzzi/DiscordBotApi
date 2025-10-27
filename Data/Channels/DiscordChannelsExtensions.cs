using DiscordBotApi.Data.Guilds;
using NetCord;

namespace DiscordBotApi.Data.Channels
{
    public static class DiscordChannelsExtensions
    {
        public static IEnumerable<DiscordChannel> Convert(this IEnumerable<IGuildChannel> channels, DiscordGuild guild)
        {
            foreach (var channel in channels)
            {
                yield return new()
                {
                    Id = channel.Id,
                    Name = channel.Name,
                    IsTextChannel = channel is not VoiceGuildChannel,
                    Guild = guild
                };
            }
        }
    }
}
