using DiscordBotApi.DiscordBot.Services.Secrets;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace DiscordBotApi.DiscordBot.Services
{
    public static class AuthorizationAdminUsersService
    {
        public static async Task<bool> IsAuthorizedRoleOrUser(this ApplicationCommandModule<ApplicationCommandContext> module)
        {
            var guildUser = module.Context.User as GuildUser;

            if (!SecretsLoader.TryGetSecrets(out var secrets))
            {
                InteractionMessageProperties imsgp = new()
                {
                    Content = "Ошибка загрузки ролей",
                    Flags = MessageFlags.Ephemeral
                };
                var errorMsg = InteractionCallback.Message(imsgp);

                await module.RespondAsync(errorMsg);
                return false;
            }

            if (guildUser == null || module.Context.Guild == null)
            {
                InteractionMessageProperties imsgp = new()
                {
                    Content = "Ошибка роли пользователя",
                    Flags = MessageFlags.Ephemeral
                };
                var errorMsg = InteractionCallback.Message(imsgp);

                await module.RespondAsync(errorMsg);
                return false;
            }

            var roles = guildUser.GetRoles(module.Context.Guild);
            bool isAllowed = false;
            foreach (var role in roles)
            {
                if (HasId(secrets, role.Id, guildUser.Id))
                {
                    isAllowed = true;
                    break;
                }
            }

            if (!isAllowed)
            {
                InteractionMessageProperties imsgp = new()
                {
                    Content = "Роль пользователя не авторизована",
                    Flags= MessageFlags.Ephemeral
                };
                var errorMsg = InteractionCallback.Message(imsgp);

                await module.RespondAsync(errorMsg);
                return false;
            }

            return true;
        }

        public static async Task<bool> IsAuthorizedRoleOrUser(this ComponentInteractionModule<ButtonInteractionContext> module)
        {
            var guildUser = module.Context.User as GuildUser;

            if (!SecretsLoader.TryGetSecrets(out var secrets))
            {
                InteractionMessageProperties imsgp = new()
                {
                    Content = "Ошибка загрузки ролей",
                    Flags = MessageFlags.Ephemeral
                };
                var errorMsg = InteractionCallback.Message(imsgp);

                await module.RespondAsync(errorMsg);
                return false;
            }

            if (guildUser == null || module.Context.Guild == null)
            {
                InteractionMessageProperties imsgp = new()
                {
                    Content = "Ошибка роли пользователя",
                    Flags = MessageFlags.Ephemeral
                };
                var errorMsg = InteractionCallback.Message(imsgp);

                await module.RespondAsync(errorMsg);
                return false;
            }

            var roles = guildUser.GetRoles(module.Context.Guild);
            bool isAllowed = false;
            foreach (var role in roles)
            {
                if (HasId(secrets, role.Id, guildUser.Id))
                {
                    isAllowed = true;
                    break;
                }
            }

            if (!isAllowed)
            {
                InteractionMessageProperties imsgp = new()
                {
                    Content = "Роль пользователя не авторизована",
                    Flags = MessageFlags.Ephemeral
                };
                var errorMsg = InteractionCallback.Message(imsgp);

                await module.RespondAsync(errorMsg);
                return false;
            }

            return true;
        }

        private static bool HasId(SecretsJson secrets, ulong id)
        {
            for (int i = 0; i < secrets.AdminIds.Length; i++)
            {
                if (secrets.AdminIds[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasId(SecretsJson secrets, ulong roleId, ulong userId)
        {
            for (int i = 0; i < secrets.AdminIds.Length; i++)
            {
                var adminId = secrets.AdminIds[i];
                if (adminId == roleId || adminId == userId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
