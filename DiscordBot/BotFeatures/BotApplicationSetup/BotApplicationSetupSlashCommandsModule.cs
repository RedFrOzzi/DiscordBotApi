using DiscordBotApi.Data.Channels;
using DiscordBotApi.Data.DiscordUsers;
using DiscordBotApi.Data.Guilds;
using DiscordBotApi.Data.Roles;
using DiscordBotApi.Data.Settings;
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

        SaveOrUpdateUsers(_dbContext, guildUsers);

        var updatedUsers = _dbContext.DiscordUsers.Include(u => u.Guilds).AsEnumerable().DistinctBy(u => u.Id).ToDictionary(u => u.Id);
        var owner = updatedUsers?.FirstOrDefault(u => u.Key == guild.OwnerId).Value;
        var updatedUsersList = updatedUsers?.Where(u => guildUsers.ContainsKey(u.Key)).Select(kvp => kvp.Value).ToList();

        if (owner == null)
        {
            await ModifyResponseAsync(m => m.Content = "Ошибка: владелец канала не найден");
            return;
        }

        SaveOrUpdateGuild(_dbContext, guild, updatedUsersList, owner);

        await Task.Delay(5000);

        var channels = await _gatewayClient.Rest.GetGuildChannelsAsync(Context.Guild.Id);
        SaveOrUpdateChannels(_dbContext, channels, guild);

        await Task.Delay(5000);

        var roles = await _gatewayClient.Rest.GetGuildRolesAsync(Context.Guild.Id);
        SaveOrUpdateRoles(_dbContext, roles, guildUsers, updatedUsersList, guild.Id);

        await ModifyResponseAsync(m => m.Content = "Данные обновлены");
    }

    [SubSlashCommand("добавить_роль", "Добавить роль, которая имеет право создавать игры")]
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

        if (CreateRaffleSettingsOrAddRole(_dbContext, Context.Guild.Id, role.Id))
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

    [SubSlashCommand("убрать_роль", "Убрать роль, которая имеет право создавать игры")]
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

        if (RemoveRoleFromRaffleSettings(_dbContext, Context.Guild.Id, role.Id))
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

    private static void SaveOrUpdateUsers(ApplicationDbContext context, Dictionary<ulong, GuildUser> users)
    {
        if (context == null || users == null || users.Count == 0)
            return;

        try
        {
            var convertedUsers = users.ConvertToDiscordUsers();
            List<ulong> guildUserIds = [.. users.Keys];
            var existingUsers = context.DiscordUsers.Where(u => guildUserIds.Contains(u.Id)).AsEnumerable().DistinctBy(u => u.Id).ToDictionary(u => u.Id);
            List<DiscordUser> newUsers = [];
            for (int i = 0; i < convertedUsers.Count; i++)
            {
                var id = convertedUsers[i].Id;
                if (existingUsers.TryGetValue(convertedUsers[i].Id, out DiscordUser? value))
                {
                    value.Username = convertedUsers[i].Username;
                    value.GlobalName = convertedUsers[i].GlobalName;
                    value.Nickname = convertedUsers[i].Nickname;
                    continue;
                }

                newUsers.Add(convertedUsers[i]);
            }

            if (newUsers.Count > 0)
                context.DiscordUsers.AddRange(newUsers);

            context.SaveChanges();
        }
        catch { }
    }

    private static void SaveOrUpdateGuild(
        ApplicationDbContext context,
        RestGuild guild,
        List<DiscordUser>? updatedUsersList,
        DiscordUser owner)
    {
        try
        {
            var dbGuild = context.Guilds.FirstOrDefault(u => u.Id == guild.Id);
            if (dbGuild == null)
            {
                DiscordGuild newGuild = new()
                {
                    Id = guild.Id,
                    Name = guild.Name,
                    Owner = owner,
                    Users = updatedUsersList ?? []
                };

                context.Guilds.Add(newGuild);
            }
            else
            {
                dbGuild.Name = guild.Name;
                dbGuild.Owner = owner;

                if (updatedUsersList != null && updatedUsersList.Count > 0)
                {
                    foreach (var newUser in updatedUsersList)
                    {
                        if (newUser.Guilds.Any(g => g.Id == dbGuild.Id))
                            continue;

                        newUser.Guilds.Add(dbGuild);
                    }
                }
            }

            context.SaveChanges();
        }
        catch { }
    }

    private static void SaveOrUpdateChannels(ApplicationDbContext context, IReadOnlyList<IGuildChannel> channels, RestGuild guild)
    {
        var dbGuild = context.Guilds.FirstOrDefault(g => g.Id == guild.Id);
        if (dbGuild == null || channels == null || channels.Count == 0)
            return;

        var channelsDict = channels.DistinctBy(c => c.Id).ToDictionary(c => c.Id);
        var convertedChannels = channels.Convert(dbGuild);
        List<ulong> channelIds = [.. channelsDict.Keys];
        var existingChannels = context.Channels.Where(u => channelIds.Contains(u.Id)).AsEnumerable().DistinctBy(u => u.Id).ToDictionary(u => u.Id);
        List<DiscordChannel> newChannels = [];
        for (int i = 0; i < convertedChannels.Count; i++)
        {
            var id = convertedChannels[i].Id;
            if (existingChannels.TryGetValue(convertedChannels[i].Id, out DiscordChannel? value))
            {
                value.Name = convertedChannels[i].Name;
                value.IsTextChannel = convertedChannels[i].IsTextChannel;
                value.Guild = dbGuild;
                continue;
            }

            newChannels.Add(convertedChannels[i]);
        }

        if (newChannels.Count > 0)
            context.Channels.AddRange(newChannels);

        context.SaveChanges();
    }

    private static void SaveOrUpdateRoles(
        ApplicationDbContext context,
        IReadOnlyList<NetCord.Role>? roles,
        Dictionary<ulong, GuildUser> guildUsers,
        List<DiscordUser>? updatedUsersList,
        ulong guildId)
    {
        DiscordGuild? guild = context.Guilds.FirstOrDefault(g => g.Id == guildId);
        if (roles == null || roles.Count == 0 || guild == null)
            return;

        var newRoles = roles.Convert(guild);
        var dbRoles1 = context.DiscordGuildRoles
            .Include(r => r.DiscordUsersWithRole)
            .Where(r => r.DiscordGuild.Id == guildId)
            .ToList();
        List<DiscordGuildRole> insertRoles = [.. newRoles];

        foreach (var role in newRoles)
        {
            foreach (var r in dbRoles1)
            {
                if (role.Id == r.Id)
                {
                    insertRoles.Remove(role);
                }
            }
        }

        context.DiscordGuildRoles.AddRange(insertRoles);
        context.SaveChanges();

        var dbRoles = context.DiscordGuildRoles
            .Include(r => r.DiscordUsersWithRole)
            .Where(r => r.DiscordGuild.Id == guildId)
            .ToList();
        Dictionary<ulong, List<ulong>> userIdToRoleIds = [];
        foreach (var u in guildUsers.Values)
        {
            if (u.RoleIds == null || u.RoleIds.Count == 0)
                continue;

            List<ulong> roleIds = [.. u.RoleIds];
            userIdToRoleIds.TryAdd(u.Id, roleIds);
        }

        var roleIdToUserIds = new Dictionary<ulong, List<ulong>>();

        foreach (var kvp in userIdToRoleIds)
        {
            ulong userId = kvp.Key;
            foreach (var roleId in kvp.Value)
            {
                if (!roleIdToUserIds.TryGetValue(roleId, out List<ulong>? userList))
                {
                    userList = [];
                    roleIdToUserIds[roleId] = userList;
                }
                userList.Add(userId);
            }
        }

        foreach (var r in roleIdToUserIds)
        {
            var dbRole = dbRoles.FirstOrDefault(dbr => dbr.Id == r.Key);
            if (dbRole == null)
                continue;

            var users = updatedUsersList?.Where(u => r.Value.Contains(u.Id)).ToList();
            if (users == null || users.Count == 0)
                continue;

            foreach (var user in users)
            {
                if (dbRole.DiscordUsersWithRole.Any(du => du.Id == user.Id))
                    continue;

                dbRole.DiscordUsersWithRole.Add(user);
            }
        }

        context.SaveChanges();
    }

    private static bool CreateRaffleSettingsOrAddRole(ApplicationDbContext ctx, ulong guildId, ulong roleId)
    {
        var guild = ctx.Guilds.FirstOrDefault(g => g.Id == guildId);
        if (guild == null)
            return false;

        var role = ctx.DiscordGuildRoles.FirstOrDefault(r => r.Id == roleId);
        if (role == null)
            return false;

        var dbSettings = ctx.Settings
            .Include(r => r.PrivilegedRoles)
            .FirstOrDefault(rs => rs.Guild.Id == guildId);

        //Create new entity if empty
        if (dbSettings == null)
        {
            var settings = new Settings()
            {
                Guild = guild,
                PrivilegedRoles = [role]
            };

            ctx.Settings.Add(settings);
            return ctx.SaveChanges() > 0;
        }

        if (dbSettings.PrivilegedRoles == null)
        {
            dbSettings.PrivilegedRoles = [role];
            return ctx.SaveChanges() > 0;
        }

        //Add role if exist
        dbSettings.PrivilegedRoles.Add(role);
        return ctx.SaveChanges() > 0;
    }

    private static bool RemoveRoleFromRaffleSettings(ApplicationDbContext ctx, ulong guildId, ulong roleId)
    {
        var guild = ctx.Guilds.FirstOrDefault(g => g.Id == guildId);
        if (guild == null)
            return false;

        var role = ctx.DiscordGuildRoles.FirstOrDefault(r => r.Id == roleId);
        if (role == null)
            return false;

        var dbRaffleSettings = ctx.Settings
            .Include(r => r.PrivilegedRoles)
            .FirstOrDefault(rs => rs.Guild.Id == guildId);

        if (dbRaffleSettings == null || dbRaffleSettings.PrivilegedRoles == null || dbRaffleSettings.PrivilegedRoles.Count == 0)
            return false;

        //returns false if role was not found in collection
        if (!dbRaffleSettings.PrivilegedRoles.Remove(role))
            return false;

        return ctx.SaveChanges() > 0;
    }
}
