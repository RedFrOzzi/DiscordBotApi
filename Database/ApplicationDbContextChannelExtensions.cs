using DiscordBotApi.Data.Channels;
using DiscordBotApi.Data.Guilds;
using Microsoft.EntityFrameworkCore;
using NetCord;

namespace DiscordBotApi.Database
{
    public static class ApplicationDbContextChannelExtensions
    {
        public static async Task<List<DiscordChannel>> GetChannelsAsync(this ApplicationDbContext ctx, ulong guildId, CancellationToken cancellationToken)
        {
            return await ctx.Channels.Where(c => c.Guild.Id == guildId).ToListAsync(cancellationToken);
        }

        public static async Task<List<DiscordChannelGetDto>> GetChannelDtosAsync(this ApplicationDbContext ctx, CancellationToken cancellationToken)
        {
            return await ctx.Channels.Select(c => new DiscordChannelGetDto()
            {
                Id = c.Id.ToString(),
                Name = c.Name,
                IsTextChannel = c.IsTextChannel,
                GuildId = c.Guild.Id.ToString(),
            })
                .ToListAsync(cancellationToken);
        }

        public static bool SaveNewChannelsData(this ApplicationDbContext ctx, DiscordGuild guild, IReadOnlyList<IGuildChannel> channels)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                if (ctx.Channels.FirstOrDefault(c => c.Id == channels[i].Id) == null)
                {
                    continue;
                }

                DiscordChannel dChannel = new()
                {
                    Id = channels[i].Id,
                    Name = channels[i].Name,
                    IsTextChannel = channels[i] is not VoiceGuildChannel,
                    Guild = guild,
                };

                ctx.Channels.Add(dChannel);
            }

            var changes = ctx.SaveChanges();
            return changes > 0;
        }

        public static bool UpdateChannelsData(this ApplicationDbContext ctx, DiscordGuild guild, IReadOnlyList<IGuildChannel> channels)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                var foundChannel = ctx.Channels.FirstOrDefault(c => c.Id == channels[i].Id);
                if (foundChannel == null)
                {
                    continue;
                }

                foundChannel.Name = channels[i].Name;
                foundChannel.Guild = guild;
                foundChannel.IsTextChannel = channels[i] is not VoiceGuildChannel;
                ctx.Channels.Update(foundChannel);
            }

            var changes = ctx.SaveChanges();
            return changes > 0;
        }
    }
}
