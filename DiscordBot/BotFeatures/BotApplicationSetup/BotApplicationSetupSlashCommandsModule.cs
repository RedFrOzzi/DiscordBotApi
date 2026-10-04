using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities;
using DiscordBotApi.Utilities.Result;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordBotApi.DiscordBot.BotFeatures.BotApplicationSetup;

[SlashCommand("настройки", "Настроить бота")]
public class BotApplicationSetupSlashCommandsModule(ApplicationDbContext dbContext, GatewayClient gatewayClient) : ApplicationCommandModule<ApplicationCommandContext>
{
    readonly ApplicationDbContext _dbContext = dbContext;
    readonly GatewayClient _gatewayClient = gatewayClient;

    [SubSlashCommand("сохранить_данные", "Сохраняет данные канала")]
    public async Task CollectData()
    {
        //ack msg
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        var adminIds = Environment.GetEnvironmentVariablesArrayAsUlong("ADMIN_IDS");
        if (Context.User.Id != Context.Guild?.OwnerId && !adminIds.Contains(Context.User.Id))
        {
            await ModifyResponseAsync(m => m.Content = "Отказано в доступе");
            return;
        }

        if (Context.Guild == null)
        {
            await ModifyResponseAsync(m => m.Content = "Внутренняя ошибка");
            return;
        }

        var guild = await _gatewayClient.Rest.GetGuildAsync(Context.Guild.Id);
        if (guild == null)
        {
            await ModifyResponseAsync(m => m.Content = "Ошибка: канал не найден");
            return;
        }

        Dictionary<ulong, GuildUser> guildUsers = [];
        await foreach (var u in _gatewayClient.Rest.GetGuildUsersAsync(Context.Guild.Id))
        {
            guildUsers.Add(u.Id, u);
        }

        SaveDiscordDataUtils.SaveOrUpdateUsers(_dbContext, guildUsers);

        var updatedUsers = _dbContext.DiscordUsers.Include(u => u.Guilds).AsEnumerable().DistinctBy(u => u.Id).ToDictionary(u => u.Id);
        var owner = updatedUsers?.FirstOrDefault(u => u.Key == guild.OwnerId).Value;
        var updatedUsersList = updatedUsers?.Where(u => guildUsers.ContainsKey(u.Key)).Select(kvp => kvp.Value).ToList();

        if (owner == null)
        {
            await ModifyResponseAsync(m => m.Content = "Ошибка: владелец канала не найден");
            return;
        }

        SaveDiscordDataUtils.SaveOrUpdateGuild(_dbContext, guild, updatedUsersList, owner);

        await Task.Delay(5000);

        var channels = await _gatewayClient.Rest.GetGuildChannelsAsync(Context.Guild.Id);
        SaveDiscordDataUtils.SaveOrUpdateChannels(_dbContext, channels, guild);

        await Task.Delay(5000);

        var roles = await _gatewayClient.Rest.GetGuildRolesAsync(Context.Guild.Id);
        SaveDiscordDataUtils.SaveOrUpdateRoles(_dbContext, roles, guildUsers, updatedUsersList, guild.Id);

        await ModifyResponseAsync(m => m.Content = "Данные обновлены");
    }

    [SubSlashCommand("добавить_роль", "Добавить доступ к приложению для роли")]
    public async Task AddRoleToRaffleSettings(Role role)
    {
        var result = await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext);
        if (result is Error)
        {
            InteractionMessageProperties imsgp = new()
            {
                Content = result.Message,
                Flags = MessageFlags.Ephemeral
            };
            var errorMsg = InteractionCallback.Message(imsgp);

            await RespondAsync(errorMsg);
            return;
        }

        if (role == null)
        {
            InteractionMessageProperties errorMsgProps = new()
            {
                Content = "Введенные данные не верны",
                Flags = MessageFlags.Ephemeral
            };
            await RespondAsync(InteractionCallback.Message(errorMsgProps));
            return;
        }

        if (Context.Guild == null)
        {
            InteractionMessageProperties errorMsgProps = new()
            {
                Content = "Внутренняя ошибка",
                Flags = MessageFlags.Ephemeral
            };
            await RespondAsync(InteractionCallback.Message(errorMsgProps));
            return;
        }

        if (SaveDiscordDataUtils.CreateRaffleSettingsOrAddRole(_dbContext, Context.Guild.Id, role.Id))
        {
            InteractionMessageProperties imsgp = new()
            {
                Content = "Настройки сохранены",
                Flags = MessageFlags.Ephemeral
            };
            var errorMsg = InteractionCallback.Message(imsgp);

            await RespondAsync(errorMsg);
            return;
        }

        InteractionMessageProperties errorMsgProps2 = new()
        {
            Content = "Ошибка сохранения настроек",
            Flags = MessageFlags.Ephemeral
        };
        await RespondAsync(InteractionCallback.Message(errorMsgProps2));
    }

    [SubSlashCommand("убрать_роль", "Убрать доступ к приложению для роли")]
    public async Task RemoveRoleToRaffleSettings(Role role)
    {
        var result = await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext);
        if (result is Error)
        {
            InteractionMessageProperties imsgp = new()
            {
                Content = result.Message,
                Flags = MessageFlags.Ephemeral
            };
            var errorMsg = InteractionCallback.Message(imsgp);

            await RespondAsync(errorMsg);
            return;
        }

        if (role == null)
        {
            InteractionMessageProperties errorMsgProps = new()
            {
                Content = "Введенные данные не верны",
                Flags = MessageFlags.Ephemeral
            };
            await RespondAsync(InteractionCallback.Message(errorMsgProps));
            return;
        }

        if (Context.Guild == null)
        {
            InteractionMessageProperties errorMsgProps = new()
            {
                Content = "Внутренняя ошибка",
                Flags = MessageFlags.Ephemeral
            };
            await RespondAsync(InteractionCallback.Message(errorMsgProps));
            return;
        }

        if (SaveDiscordDataUtils.RemoveRoleFromRaffleSettings(_dbContext, Context.Guild.Id, role.Id))
        {
            InteractionMessageProperties imsgp = new()
            {
                Content = "Настройки сохранены",
                Flags = MessageFlags.Ephemeral
            };
            var errorMsg = InteractionCallback.Message(imsgp);

            await RespondAsync(errorMsg);
            return;
        }

        InteractionMessageProperties errorMsgProps2 = new()
        {
            Content = "Ошибка сохранения настроек",
            Flags = MessageFlags.Ephemeral
        };
        await RespondAsync(InteractionCallback.Message(errorMsgProps2));
    }
}
