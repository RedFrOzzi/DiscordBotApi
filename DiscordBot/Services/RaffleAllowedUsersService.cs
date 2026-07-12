using DiscordBotApi.Database;
using DiscordBotApi.Utilities.Result;
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

        var allowedRoles = dbContext.GetAllowedRoleIds(context.Guild.Id);

        if (allowedRoles == null)
            return new Error("Не настроены роли для пользователей");

        var roles = guildUser.GetRoles(context.Guild);
        foreach (var role in roles)
        {
            if (allowedRoles.Contains(role.Id))
                return Success.Empty;
        }

        return new Error("Роль пользователя не авторизована");
    }
}
