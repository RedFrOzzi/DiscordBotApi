using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities;
using DiscordBotApi.Utilities.Result;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordBotApi.DiscordBot.BotFeatures.RaffleService
{
    [SlashCommand("игра", "Игра со ставками")]
    public class RaffleSlashCommandsModule : ApplicationCommandModule<ApplicationCommandContext>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly RaffleAllowedUsersService _raffleAllowedUsersService;

        public RaffleSlashCommandsModule(ApplicationDbContext dbContext, RaffleAllowedUsersService raffleAllowedUsersService)
        {
            _dbContext = dbContext;
            _raffleAllowedUsersService = raffleAllowedUsersService;
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

            var user = _dbContext.GetUser(Context.User.Id);
            if (user == null)
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Тебя нет",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            var userBets = _dbContext.GetUserBets(user.Id);
            var allBetsAmount = userBets.Sum(ub => ub.BetAmount);
            var maxBet = userBets.Max(ub => ub.BetAmount);
            var latest = userBets.Aggregate((current, next) =>
                current.CreatedAt > next.CreatedAt ? current : next);

            InteractionMessageProperties respondMsgProps = new()
            {
                Flags = MessageFlags.Ephemeral,
            };

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

            respondMsgProps.Embeds = [embedProps];
            await RespondAsync(InteractionCallback.Message(respondMsgProps));
        }
    }
}
