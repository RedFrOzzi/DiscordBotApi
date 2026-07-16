using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using DiscordBotApi.Utilities.Result;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Services;

namespace DiscordBotApi.DiscordBot.Services;

public class RaffleAllowedUsersService
{
    public async Task<Result> IsAuthorizedRoleOrOwner<TContext>(TContext context, ApplicationDbContext dbContext) where TContext : IUserContext, IGuildContext
    {
        var guildUser = context.User as GuildUser;
        if (guildUser == null || context.Guild == null)
            return new Error("Ошибка роли пользователя");

        if (guildUser.Id == context.Guild.OwnerId)
            return Success.Empty;

        var adminIds = Environment.GetEnvironmentVariablesArrayAsUlong("ADMIN_IDS");
        if (adminIds != null && adminIds.Count > 0 && adminIds.Contains(guildUser.Id))
            return Success.Empty;

        var allowedRoleIds = dbContext.RaffleSettings
            .AsNoTracking()
            .Include(rs => rs.AllowedRoles)
            .FirstOrDefault(rs => rs.Guild.Id == context.Guild.Id)
            ?.AllowedRoles
            .Select(ar => ar.Id)
            .ToArray();

        if (allowedRoleIds == null)
            return new Error("Не настроены роли для пользователей");

        var roles = guildUser.GetRoles(context.Guild);
        foreach (var role in roles)
        {
            if (allowedRoleIds.Contains(role.Id))
                return Success.Empty;
        }

        return new Error("Роль пользователя не авторизована");
    }
}
