using DiscordBotApi.Data.Roles;
using Microsoft.EntityFrameworkCore;

namespace DiscordBotApi.Database;

public static class ApplicationDbContextRoleExtensions
{
    public static IEnumerable<DiscordGuildRole> GetGuildRoles(this ApplicationDbContext ctx, ulong guildId)
    {
        return ctx.DiscordChannelRole
            .AsNoTracking()
            .Where(r => r.DiscordGuild.Id == guildId)
            .AsEnumerable();
    }

    public static IEnumerable<DiscordGuildRole> GetUserGuildRoles(this ApplicationDbContext ctx, ulong guildId, ulong userId)
    {
        var discordUser = ctx.GetUser(userId);
        if (discordUser == null)
            return [];

        return ctx.DiscordChannelRole
            .AsNoTracking()
            .Where(r => r.DiscordGuild.Id == guildId &&
                r.DiscordUsersWithRole != null &&
                r.DiscordUsersWithRole.Contains(discordUser))
            .AsEnumerable();
    }
}
