using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities.Result;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordBotApi.DiscordBot.BotFeatures.RaffleService;

[SlashCommand("игра", "Игра со ставками")]
public class RaffleSlashCommandsModule : ApplicationCommandModule<ApplicationCommandContext>
{
    readonly ApplicationDbContext _dbContext;
    readonly RaffleAllowedUsersService _raffleAllowedUsersService;

    public RaffleSlashCommandsModule(ApplicationDbContext dbContext, RaffleAllowedUsersService raffleAllowedUsersService)
    {
        _dbContext = dbContext;
        _raffleAllowedUsersService = raffleAllowedUsersService;
    }

    [SubSlashCommand("добавить_роль", "Добавить роль, которая имеет право создавать игры")]
    public async Task AddRoleToRaffleSettings(Role role)
    {
        var result = await _raffleAllowedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext);
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
        var result = await _raffleAllowedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext);
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

    [SubSlashCommand("создать", "Создать вопрос для игры")]
    public async Task CreateRaffleModal()
    {
        var result = await _raffleAllowedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext);
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

        ModalProperties mProps = new(RaffleConstants.RaffleInitialModalId, "Создание игры")
        {
            Components = [
                new LabelProperties("Вопрос", new TextInputProperties(RaffleConstants.ModalQuestionId, TextInputStyle.Paragraph)
                {
                    Placeholder = "Вопрос",
                    Required = true,
                }),
                new LabelProperties("Ответ №1", new TextInputProperties(RaffleConstants.ModalAnswer_1_Id, TextInputStyle.Short)
                {
                    Placeholder = "Ответ №1",
                    Required = true,
                }),
                new LabelProperties("Ответ №2", new TextInputProperties(RaffleConstants.ModalAnswer_2_Id, TextInputStyle.Short)
                {
                    Placeholder = "Ответ №2",
                    Required = true,
                }),
                new LabelProperties("Ответ №3 (не обязательный)", new TextInputProperties(RaffleConstants.ModalAnswer_3_Id, TextInputStyle.Short)
                {
                    Placeholder = "Ответ №3 (не обязательный)",
                    Required = false,
                }),
                new LabelProperties("Ответ №4 (не обязательный)", new TextInputProperties(RaffleConstants.ModalAnswer_4_Id, TextInputStyle.Short)
                {
                    Placeholder = "Ответ №4 (не обязательный)",
                    Required = false,
                })]
        };

        var modal = InteractionCallback.Modal(mProps);

        await RespondAsync(modal);
    }

    [SubSlashCommand("статистика", "Узнать свою статистику")]
    public async Task GetRaffleStats()
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        var user = _dbContext.DiscordUsers
            .AsNoTracking()
            .FirstOrDefault(u => u.Id == Context.User.Id);

        if (user == null)
        {
            await ModifyResponseAsync(m => m.Content = "Тебя нет");
            return;
        }

        var userBets = _dbContext.UserBets
            .AsNoTracking()
            .Where(ub => ub.User.Id == user.Id)
            .Select(ub => new UserBet
            {
                Id = ub.Id,
                AnswerNumber = ub.AnswerNumber,
                BetAmount = ub.BetAmount,
                CreatedAt = ub.CreatedAt,
            })
            .ToArray();

        if (userBets == null || userBets.Length > 0)
        {
            await ModifyResponseAsync(m => m.Content = "Нет статистики по ставкам");
            return;
        }

        var allBetsAmount = userBets.Sum(ub => ub.BetAmount);
        var maxBet = userBets.Max(ub => ub.BetAmount);
        var latest = userBets.Aggregate((current, next) =>
            current.CreatedAt > next.CreatedAt ? current : next);

        EmbedFieldProperties embedField = new()
        {
            Name = $"Ресурс: {user.UserSpendingResource} очков",
        };

        EmbedFieldProperties embedField1 = new()
        {
            Name = "Всего ставок:",
            Value = $"{userBets.Length} на {allBetsAmount} очков",
        };

        EmbedFieldProperties embedField2 = new()
        {
            Name = "Максимальная ставка:",
            Value = $"{maxBet} очков",
        };

        EmbedFieldProperties? embedField3 = null;
        if (latest != null)
        {
            embedField3 = new()
            {
                Name = "Последняя ставка:",
                Value = $"Ответ: {latest.AnswerNumber}, ставка: {latest.BetAmount}",
            };
        }

        EmbedProperties embedProps = new()
        {
            Title = "Статистика ставок",
            Fields = [embedField, embedField1, embedField2],
            Color = new(0x20750f)
        };

        if (embedField3 != null)
        {
            embedProps.AddFields(embedField3);
        }

        await ModifyResponseAsync(m => 
        {
            m.Embeds = [embedProps];
        });
    }


    private static bool CreateRaffleSettingsOrAddRole(ApplicationDbContext ctx, ulong guildId, ulong roleId)
    {
        var guild = ctx.Guilds.FirstOrDefault(g => g.Id == guildId);
        if (guild == null)
            return false;

        var role = ctx.DiscordGuildRoles.FirstOrDefault(r => r.Id == roleId);
        if (role == null)
            return false;

        var dbRaffleSettings = ctx.RaffleSettings
            .Include(r => r.AllowedRoles)
            .FirstOrDefault(rs => rs.Guild.Id == guildId);

        //Create new entity if empty
        if (dbRaffleSettings == null)
        {
            var raffleSettings = new RaffleSettings()
            {
                Guild = guild,
                AllowedRoles = [role]
            };

            ctx.RaffleSettings.Add(raffleSettings);
            return ctx.SaveChanges() > 0;
        }

        //Add role if exist
        dbRaffleSettings.AllowedRoles.Add(role);
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

        var dbRaffleSettings = ctx.RaffleSettings
            .Include(r => r.AllowedRoles)
            .FirstOrDefault(rs => rs.Guild.Id == guildId);

        if (dbRaffleSettings == null)
            return false;

        //returns false if role was not found in collection
        if (!dbRaffleSettings.AllowedRoles.Remove(role))
            return false;

        return ctx.SaveChanges() > 0;
    }
}
