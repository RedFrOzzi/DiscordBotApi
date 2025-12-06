using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordBotApi.DiscordBot.Services.BotFeatures.RaffleService
{
    [SlashCommand("игра", "Игра со ставками")]
    public class RaffleSlashCommandsModule : ApplicationCommandModule<ApplicationCommandContext>
    {
        private readonly IServiceProvider _serviceProvider;

        public RaffleSlashCommandsModule(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        [SubSlashCommand("создать", "Создать вопрос для игры")]
        public async Task CreateRaffleModal()
        {
            if (!await this.IsAuthorizedRoleOrUser()) { return; }

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

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            if (dbContext == null)
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Внутренняя ошибка",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            var user = dbContext.Users.FirstOrDefault(u => u.Id == Context.User.Id);
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

            var userBets = dbContext.UserBets
                .Where(ub => ub.User.Id == Context.User.Id)
                .Select(ub => new UserBet
                {
                    Id = ub.Id,
                    AnswerNumber = ub.AnswerNumber,
                    BetAmount = ub.BetAmount,
                    CreatedAt = ub.CreatedAt,
                })
                .ToArray();
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
                Name = $"IQ: {user.UserIQ} поинтов",
            };

            EmbedFieldProperties embedField1 = new()
            {
                Name = "Всего ставок:",
                Value = $"{userBets.Length} на {allBetsAmount} поинтов",
            };

            EmbedFieldProperties embedField2 = new()
            {
                Name = "Максимальная ставка:",
                Value = $"{maxBet} поинтов",
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
