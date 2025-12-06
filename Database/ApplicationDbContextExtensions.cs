using DiscordBotApi.Data.Channels;
using DiscordBotApi.Data.Guilds;
using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Data.Users;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using System.Threading.Tasks;

namespace DiscordBotApi.Database
{
    public static class ApplicationDbContextExtensions
    {
        //API USER
        public static bool CreateApiUser(this ApplicationDbContext ctx, string login, string passwordHash)
        {
            var user = new ApiUser
            {
                Login = login,
                PasswordHash = passwordHash
            };

            ctx.ApiUsers.Add(user);
            return ctx.SaveChanges() > 0;
        }

        public static ApiUser? GetApiUser(this ApplicationDbContext ctx, string login)
        {
            return ctx.ApiUsers.FirstOrDefault(x => x.Login == login);
        }

        //DISCORD USER
        public static DiscordUser? GetUser(this ApplicationDbContext ctx, ulong id)
        {
            return ctx.Users.FirstOrDefault(ctx => ctx.Id == id);
        }

        public static async Task<List<DiscordUser>> GetUsers(this ApplicationDbContext ctx, CancellationToken cancellationToken)
        {
            return await ctx.Users.ToListAsync(cancellationToken);
        }

        //GUILDS
        public static async Task<DiscordGuild?> GetGuild(this ApplicationDbContext ctx, ulong id, CancellationToken cancellationToken)
        {
            return await ctx.Guilds.FirstOrDefaultAsync(g => g.Id == id, cancellationToken: cancellationToken);
        }

        public static bool SaveUser(this ApplicationDbContext ctx, DiscordUser user)
        {
            ctx.Users.Add(user);

            var changes = ctx.SaveChanges();
            return changes > 0;
        }

        public static bool SaveNewUsersData(this ApplicationDbContext ctx, List<GuildUser> users)
        {
            var oldUsersIds = ctx.Users.Select(u => u.Id).ToArray();

            var newUsers = users.Where(u => !oldUsersIds.Contains(u.Id)).ToArray();

            for (int i = 0; i < newUsers.Length; i++)
            {
                var dUser = newUsers[i].ConvertToDiscordUser();
                ctx.Users.Add(dUser);
            }

            var changes = ctx.SaveChanges();
            return changes > 0;
        }

        public static bool TryUpdateUsers(this ApplicationDbContext ctx, List<GuildUser> users)
        {
            try
            {
                for (int i = 0; i < users.Count; i++)
                {
                    var discordUser = ctx.Users.FirstOrDefault(u => u.Id == users[i].Id);
                    if (discordUser == null) { continue; }
                    discordUser.Nickname = users[i].Nickname;
                    discordUser.GlobalName = users[i].GlobalName;
                }

                var changes = ctx.SaveChanges();
                return changes > 0;
            }
            catch
            {
                return false;
            }
        }

        //GUILD
        public static bool IsGuildExistInDb(this ApplicationDbContext ctx, ulong guildId, out DiscordGuild? guild)
        {
            guild = ctx.Guilds.FirstOrDefault(g => g.Id == guildId);
            if (guild == null)
            {
                return false;
            }

            return true;
        }

        public static bool SaveGuildData(this ApplicationDbContext ctx,
                                         RestGuild guild,
                                         List<DiscordUser> allDbUsers,
                                         List<GuildUser> guildUsers)
        {
            List<DiscordUser> newGuildUsers = [];
            DiscordUser? owner = null;
            for (int i = 0; i < guildUsers.Count; i++)
            {
                if (ContainsUser(guildUsers[i].Id, allDbUsers, out var foundUser))
                {
                    newGuildUsers.Add(foundUser);
                    if (foundUser.Id == guild.OwnerId)
                    {
                        owner = foundUser;
                    }
                }
            }

            if (owner == null)
            {
                return false;
            }

            DiscordGuild newGuild = new()
            {
                Id = guild.Id,
                Name = guild.Name,
                Owner = owner,
                Users = newGuildUsers,
            };

            ctx.Guilds.Add(newGuild);

            var changes = ctx.SaveChanges();
            return changes > 0;
        }

        public static async Task<bool> UpdateGuildData(this ApplicationDbContext ctx,
                                         DiscordGuild localGuild,
                                         List<DiscordChannel> allDbChannels,
                                         IReadOnlyList<IGuildChannel> channels,
                                         CancellationToken cancellationToken)
        {
            List<IGuildChannel> newChannelsToAdd = [];

            for (int i = 0; i < channels.Count; i++)
            {
                if (ContainsChannel(channels[i].Id, allDbChannels, out var foundChannel))
                {
                    foundChannel.Name = channels[i].Name;
                    foundChannel.IsTextChannel = channels[i] is not VoiceGuildChannel;
                    foundChannel.Guild = localGuild;
                    ctx.Channels.Update(foundChannel);
                }
                else
                {
                    DiscordChannel newChannel = new()
                    {
                        Id= channels[i].Id,
                        Name = channels[i].Name,
                        IsTextChannel = channels[i] is not VoiceGuildChannel,
                        Guild = localGuild,
                    };
                    ctx.Channels.Add(newChannel);
                }
            }

            var changes = await ctx.SaveChangesAsync(cancellationToken);



            return changes > 0;
        }

        //CHANNELS

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

        //RAFFLE
        public static bool TryCreateNewRaffle(this ApplicationDbContext ctx, Raffle raffle, out int insertedRaffleId)
        {
            ctx.Rafles.Add(raffle);
            bool isInserted = ctx.SaveChanges() > 0;
            insertedRaffleId = raffle.Id;
            return isInserted;
        }

        public static bool TryUpdateRaffle(this ApplicationDbContext ctx, int raffleId, Raffle newValuesRaffle)
        {
            var foundRaffle = ctx.Rafles.FirstOrDefault(r => r.Id == raffleId);
            if (foundRaffle == null)
            {
                return false;
            }

            foundRaffle.Question = newValuesRaffle.Question;
            foundRaffle.Answer_1 = newValuesRaffle.Answer_1;
            foundRaffle.Answer_2 = newValuesRaffle.Answer_2;
            foundRaffle.Answer_3 = newValuesRaffle.Answer_3;
            foundRaffle.Answer_4 = newValuesRaffle.Answer_4;
            foundRaffle.AnswerButtonsMessageId = newValuesRaffle.AnswerButtonsMessageId;
            foundRaffle.IsClosed = newValuesRaffle.IsClosed;
            foundRaffle.CreatedAt = newValuesRaffle.CreatedAt;

            return ctx.SaveChanges() > 0;
        }

        public static Raffle? GetRaffle(this ApplicationDbContext ctx, int raffleId)
        {
            return ctx.Rafles.FirstOrDefault(r => r.Id == raffleId);
        }

        public static UserBet[] GetUsersBets(this ApplicationDbContext ctx, Raffle raffle)
        {
            return ctx.UserBets.Where(ub => ub.Raffle.Id == raffle.Id).Select(ub => new UserBet
            {
                Id = ub.Id,
                User = ub.User,
                AnswerNumber = ub.AnswerNumber,
                BetAmount = ub.BetAmount,
                Raffle = ub.Raffle
            }).ToArray();
        }

        public static UserBet[] GetUsersBetsByAnswerNum(this ApplicationDbContext ctx, Raffle raffle, int answerNum)
        {
            return ctx.UserBets.Where(ub => ub.Raffle.Id == raffle.Id && ub.AnswerNumber == answerNum).Select(ub => new UserBet
            {
                Id = ub.Id,
                User = ub.User,
                AnswerNumber = ub.AnswerNumber,
                BetAmount = ub.BetAmount,
                Raffle = ub.Raffle
            }).ToArray();
        }

        public static DiscordUser[] GetUsersWithBets(this ApplicationDbContext ctx, int raffleId)
        {
            return ctx.UserBets.Where(ub => ub.Raffle.Id == raffleId).Select(ub => ub.User).ToArray();
        }

        public static bool TryAddUserBet(this ApplicationDbContext ctx, UserBet bet)
        {
            ctx.UserBets.Add(bet);
            return ctx.SaveChanges() > 0;
        }

        public static void ChangeUserIqPoints(this ApplicationDbContext ctx, int userKey, int iqPoints)
        {
            var user = ctx.Users.Find(userKey);
            if (user == null)
            {
                return;
            }

            user.UserIQ += iqPoints;
            ctx.SaveChanges();
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------

        private static bool ContainsUser(ulong id, List<DiscordUser> allDbUsers, out DiscordUser discordUser)
        {
            for (int j = 0; j < allDbUsers.Count; j++)
            {
                if (id == allDbUsers[j].Id)
                {
                    discordUser = allDbUsers[j];
                    return true;
                }
            }

            discordUser = null!;
            return false;
        }

        private static bool ContainsChannel(ulong id, List<DiscordChannel> allDbChannels, out DiscordChannel foundChannel)
        {
            for (int i = 0; i < allDbChannels.Count; i++)
            {
                if (id == allDbChannels[i].Id)
                {
                    foundChannel = allDbChannels[i];
                    return true;
                }
            }

            foundChannel = null!;
            return false;
        }
    }
}
