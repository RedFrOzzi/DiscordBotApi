using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using DiscordBotApi.Utilities.Result;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Services;

namespace DiscordBotApi.DiscordBot.Services;

public class PrivilegedUsersService
{
    public static async Task<Result> IsAuthorizedRoleOrOwner<TContext>(TContext context, ApplicationDbContext dbContext) where TContext : IUserContext, IGuildContext
    {
        var guildUser = context.User as GuildUser;
        if (guildUser == null || context.Guild == null)
            return new Error("Ошибка роли пользователя");

        if (guildUser.Id == context.Guild.OwnerId)
            return Success.Empty;

        var adminIds = Environment.GetEnvironmentVariablesArrayAsUlong("ADMIN_IDS");
        if (adminIds != null && adminIds.Count > 0 && adminIds.Contains(guildUser.Id))
            return Success.Empty;

        var allowedRoleIds = dbContext.Settings
        .AsNoTracking()
        .Where(s => s.Guild.Id == context.Guild.Id)
        .SelectMany(s => s.PrivilegedRoles)
        .Select(r => r.Id)
        .ToArray();

        if (allowedRoleIds == null)
            return new Error("Не настроены роли для пользователей");

        if (guildUser.GetRoles(context.Guild).Any(r => allowedRoleIds.Contains(r.Id)))
            return Success.Empty;

        return new Error("Роль пользователя не авторизована");
    }
}
