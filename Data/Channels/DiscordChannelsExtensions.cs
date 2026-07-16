using DiscordBotApi.Data.Guilds;
using NetCord;

namespace DiscordBotApi.Data.Channels
{
    public static class DiscordChannelsExtensions
    {
        public static List<DiscordChannel> Convert(this IEnumerable<IGuildChannel> channels, DiscordGuild guild)
        {
            List<DiscordChannel> res = [];

            foreach (var channel in channels)
            {
                res.Add(new()
                {
                    Id = channel.Id,
                    Name = channel.Name,
                    IsTextChannel = channel is not VoiceGuildChannel,
                    Guild = guild
                });
            }

            return res;
        }
    }
}
