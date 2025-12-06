using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ComponentInteractions;

namespace DiscordBotApi.DiscordBot.Services.BotFeatures.RaffleService
{
    public class RaffleButtonsInteractionModule : ComponentInteractionModule<ButtonInteractionContext>
    {
        private readonly IServiceProvider _serviceProvider;

        public RaffleButtonsInteractionModule(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        [ComponentInteraction(RaffleConstants.ButtonEndRaffleId)]
        public async Task ButtonCloseRaffle(int raffleId)
        {
            if (!await this.IsAuthorizedRoleOrUser()) { return; }

            int answersCount = 2;
            foreach (var comp in Context.Message.Components)
            {
                if (comp is ActionRow row)
                {
                    answersCount = row.Components.Count > 3 ? row.Components.Count - 1 : 2;
                }
            }

            StringMenuProperties stringMenu = new($"{RaffleConstants.StringMenuCorrectAnswerSelectionId}")
            {
                new("Ответ", "1")
                {
                    Default = true,
                    Emoji = EmojiProperties.Standard("1️⃣")
                },
                new("Ответ", "2")
                {
                    Emoji = EmojiProperties.Standard("2️⃣"),
                },
            };

            stringMenu.Required = true;
            stringMenu.MinValues = 1;
            stringMenu.MaxValues = 1;

            if (answersCount > 2)
            {
                stringMenu.AddOptions([new("Ответ", "3")
                {
                    Emoji = EmojiProperties.Standard("3️⃣")
                }]);

                if (answersCount > 3)
                {
                    stringMenu.AddOptions([new("Ответ", "4")
                {
                    Emoji = EmojiProperties.Standard("4️⃣")
                }]);
                }
            }

            ModalProperties mProps = new($"{RaffleConstants.ModalCorrectAnswerSelectionId}:{raffleId}", "Правильный ответ")
            {
                Components = [ new LabelProperties("Выбор", stringMenu) ]
            };

            var callback = InteractionCallback.Modal(mProps);
            await RespondAsync(callback);
        }

        [ComponentInteraction(RaffleConstants.ButtonAnswer_1_Id)]
        public async Task ButtonAnswer_1()
        {
            await SendAnswerModal(_serviceProvider, 1);
        }

        [ComponentInteraction(RaffleConstants.ButtonAnswer_2_Id)]
        public async Task ButtonAnswer_2()
        {
            await SendAnswerModal(_serviceProvider, 2);
        }

        [ComponentInteraction(RaffleConstants.ButtonAnswer_3_Id)]
        public async Task ButtonAnswer_3()
        {
            await SendAnswerModal(_serviceProvider, 3);
        }

        [ComponentInteraction(RaffleConstants.ButtonAnswer_4_Id)]
        public async Task ButtonAnswer_4()
        {
            await SendAnswerModal(_serviceProvider, 4);
        }

        private async Task SendAnswerModal(IServiceProvider serviceProvider, int answerNum)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            if (dbContext == null)
            {
                await ReturnErrorMsg();
                return;
            }

            var userInDb = dbContext.Users.FirstOrDefault(u => u.Id == Context.User.Id);
            if (userInDb == null)
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Тебя не существует или ты что-то сломал",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            var raffle = dbContext.Rafles.FirstOrDefault(r => r.AnswerButtonsMessageId == Context.Message.Id);
            if (raffle == null)
            {
                await ReturnErrorMsg();
                return;
            }

            var alreadyBet = dbContext.UserBets.Any(b => b.Raffle.Id == raffle.Id && b.User.Id == Context.User.Id);
            if (alreadyBet)
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Уже делал ставку!",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            var maxBet = userInDb.UserIQ > 0 ? userInDb.UserIQ : 0;
            ModalProperties mProps = new($"{RaffleConstants.BetAmountModalId}:{answerNum}:{maxBet}:{raffle.Id}", $"Ставка на ответ № {answerNum}")
            {
                Components = [
                    new LabelProperties($"Не более {maxBet}", new TextInputProperties(RaffleConstants.BetAmountInputId, TextInputStyle.Paragraph)
                    {
                        Placeholder = "0",
                        Required = true,
                    })]
            };

            var calback = InteractionCallback.Modal(mProps);
            await RespondAsync(calback);


            async Task ReturnErrorMsg()
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Ошибка загрузки данных",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
            }
        }
    }
}
