using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities.Result;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordBotApi.DiscordBot.BotFeatures.RaffleService;

[SlashCommand("игра", "Игра со ставками")]
public class RaffleSlashCommandsModule(ApplicationDbContext dbContext) 
    : ApplicationCommandModule<ApplicationCommandContext>
{
    readonly ApplicationDbContext _dbContext = dbContext;

    [SubSlashCommand("создать", "Создать вопрос для игры")]
    public async Task CreateRaffleModal()
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

        if (userBets == null || userBets.Length == 0)
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
}
