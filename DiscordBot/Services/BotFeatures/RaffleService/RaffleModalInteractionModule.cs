using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace DiscordBotApi.DiscordBot.Services.BotFeatures.RaffleService
{
    public class RaffleModalInteractionModule : ComponentInteractionModule<ModalInteractionContext>
    {
        private readonly IServiceProvider _serviceProvider;

        public RaffleModalInteractionModule(IServiceProvider serviceProvider) 
        {
            _serviceProvider = serviceProvider;
        }

        //Respond to game creation command

        [ComponentInteraction(RaffleConstants.RaffleInitialModalId)]
        public async Task RespondToRaffleModal()
        {
            if (Context == null || Context.Components == null || Context.Components.Count == 0)
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Внутренняя ошибка",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            using var serviceScope = _serviceProvider.CreateScope();
            var dbContext = serviceScope.ServiceProvider.GetService<ApplicationDbContext>();

            if (dbContext == null || !dbContext.TryCreateNewRaffle(new Raffle(), out var insertedRaffleId))
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Ошибка создания",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            Raffle raffle = new();
            raffle.CreatedAt = DateTime.UtcNow.AddHours(3);

            ButtonProperties buttonEndRaffle = new($"{RaffleConstants.ButtonEndRaffleId}:{insertedRaffleId}", "Завершить", NetCord.ButtonStyle.Primary);
            ButtonProperties buttonAnswer_1 = new(RaffleConstants.ButtonAnswer_1_Id, "Ответ № 1", NetCord.ButtonStyle.Primary);
            ButtonProperties buttonAnswer_2 = new(RaffleConstants.ButtonAnswer_2_Id, "Ответ № 2", NetCord.ButtonStyle.Primary);
            ButtonProperties? buttonAnswer_3 = null;
            ButtonProperties? buttonAnswer_4 = null;

            EmbedFieldProperties? fieldAnswer_1 = null;
            EmbedFieldProperties? fieldAnswer_2 = null;
            EmbedFieldProperties? fieldAnswer_3 = null;
            EmbedFieldProperties? fieldAnswer_4 = null;

            foreach (var component in Context.Components)
            {
                if (component is not Label label || label.Component is not TextInput input) { continue; }

                if (input.CustomId == RaffleConstants.ModalQuestionId)
                {
                    raffle.Question = input.Value ?? "";
                    continue;
                }

                if (input.CustomId == RaffleConstants.ModalAnswer_1_Id)
                {
                    raffle.Answer_1 = input.Value ?? "";
                    fieldAnswer_1 = new() { Name = "Ответ № 1:", Value = $"{raffle.Answer_1}\n< Кол-во ставок: 0 | Всего вложено: 0 >" };
                    continue;
                }

                if (input.CustomId == RaffleConstants.ModalAnswer_2_Id)
                {
                    raffle.Answer_2 = input.Value ?? "";
                    fieldAnswer_2 = new() { Name = "Ответ № 2:", Value = $"{raffle.Answer_2}\n< Кол-во ставок: 0 | Всего вложено: 0 >" };
                    continue;
                }

                if (input.CustomId == RaffleConstants.ModalAnswer_3_Id)
                {
                    if (string.IsNullOrEmpty(input.Value))
                    {
                        raffle.Answer_3 = string.Empty;
                        continue;
                    }

                    raffle.Answer_3 = input.Value;
                    buttonAnswer_3 = new(RaffleConstants.ButtonAnswer_3_Id, "Ответ № 3", NetCord.ButtonStyle.Primary);
                    fieldAnswer_3 = new() { Name = "Ответ № 3:", Value = $"{raffle.Answer_3}\n< Кол-во ставок: 0 | Всего вложено: 0 >" };
                    continue;
                }

                if (input.CustomId == RaffleConstants.ModalAnswer_4_Id)
                {
                    if (string.IsNullOrEmpty(input.Value))
                    {
                        raffle.Answer_4 = string.Empty;
                        continue;
                    }

                    raffle.Answer_4 = input.Value;
                    buttonAnswer_4 = new(RaffleConstants.ButtonAnswer_4_Id, "Ответ № 4", NetCord.ButtonStyle.Primary);
                    fieldAnswer_4 = new() { Name = "Ответ № 4:", Value = $"{raffle.Answer_4}\n< Кол-во ставок: 0 | Всего вложено: 0 >" };
                    continue;
                }
            }

            InteractionMessageProperties props = new();

            ActionRowProperties actionRow = new()
            {
                Components = [buttonAnswer_1, buttonAnswer_2],
            };
            
            if (buttonAnswer_3 != null)
            {
                actionRow.AddComponents(buttonAnswer_3);
            }
            if (buttonAnswer_4 != null)
            {
                actionRow.AddComponents(buttonAnswer_4);
            }

            actionRow.AddComponents(buttonEndRaffle);

            EmbedProperties embedProps = new()
            {
                Title = raffle.Question,
                Fields = [fieldAnswer_1!, fieldAnswer_2!],
                Color = new(0xd90f3e)
            };
            if (fieldAnswer_3 != null)
            {
                embedProps.AddFields(fieldAnswer_3);
            }
            if (fieldAnswer_4 != null)
            {
                embedProps.AddFields(fieldAnswer_4);
            }

            props.Components = [actionRow];
            props.Embeds = [embedProps];
            var message = await RespondAsync(InteractionCallback.Message(props), true);

            if (message == null || message.Resource == null || message.Resource.Message == null)
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Ошибка создания",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            raffle.AnswerButtonsMessageId = message.Resource.Message.Id;

            if (!dbContext.TryUpdateRaffle(insertedRaffleId, raffle))
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Ошибка записи на диск",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }
        }

        //Respond to correct answer selection

        [ComponentInteraction(RaffleConstants.ModalCorrectAnswerSelectionId)]
        public async Task RespondToAnswerNumberSelection(int raffleId)
        {
            int answerNum = -1;
            foreach (var component in Context.Components)
            {
                if (component is not Label label || label.Component is not StringMenu menu || menu.SelectedValues?.Count < 1) { continue; }

                int.TryParse(menu.SelectedValues![0], out answerNum);
            }

            if (answerNum < 0)
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Ошибка",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            using var serviceScope = _serviceProvider.CreateScope();
            var dbContext = serviceScope.ServiceProvider.GetService<ApplicationDbContext>();

            if (dbContext == null)
            {
                InteractionMessageProperties imsgp = new()
                {
                    Content = "Внутренняя ошибка",
                    Flags = MessageFlags.Ephemeral
                };

                await RespondAsync(InteractionCallback.Message(imsgp));
                return;
            }

            var raffle = dbContext.GetRaffle(raffleId);

            if (raffle == null || raffle.IsClosed)
            {
                InteractionMessageProperties imsgp = new()
                {
                    Content = "Эта игра уже закрыта или её вообще нет",
                    Flags = MessageFlags.Ephemeral
                };

                await RespondAsync(InteractionCallback.Message(imsgp));
                return;
            }

            var userBets = dbContext.GetUsersBets(raffle);

            if (userBets.Length <= 1)
            {
                InteractionMessageProperties imsgp = new()
                {
                    Content = "Ошибка, в игре учавствует только один игрок",
                    Flags = MessageFlags.Ephemeral
                };

                await RespondAsync(InteractionCallback.Message(imsgp));
                return;
            }

            GetBetsStats(userBets, answerNum, out var totalLosersBets, out var totalWinnersBets);

            for (int i = 0; i < userBets.Length; i++)
            {
                var discordUser = userBets[i].User;
                if (discordUser == null) { continue; }

                int resourceChange = GetUserIQChange(userBets[i], answerNum, totalLosersBets, totalWinnersBets, RaffleConstants.AdditionalIQOnWin);
                userBets[i].User.UserIQ += resourceChange;
            }

            raffle.IsClosed = true;

            dbContext.SaveChanges();

            //Delete message with buttons from discord
            if (raffle.AnswerButtonsMessageId != 0)
            {
                await Context.Client.Rest.DeleteMessageAsync(Context.Channel.Id, raffle.AnswerButtonsMessageId);
            }

            InteractionMessageProperties msgProps = new()
            {
                Content = "Готово",
                Flags = MessageFlags.Ephemeral
            };

            await RespondAsync(InteractionCallback.Message(msgProps));
            return;
        }

        //Respond to user bet amount

        [ComponentInteraction(RaffleConstants.BetAmountModalId)]
        public async Task RespondToAnswerGiven(int answerNum, int maxUserBetAmount, int raffleId)
        {
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

            var raffle = dbContext.GetRaffle(raffleId);
            if (Context == null || Context.Components == null || Context.Components.Count == 0 || raffle == null)
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Внутренняя ошибка",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            var user = dbContext.GetUser(Context.User.Id);
            if (user == null)
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = $"Тебя нет",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            int bet = 0;
            foreach (var component in Context.Components)
            {
                if (component is not Label label || label.Component is not TextInput input) { continue; }

                if (!int.TryParse(input.Value, out bet) || bet < 0 || bet > maxUserBetAmount)
                {
                    InteractionMessageProperties errorMsgProps2 = new()
                    {
                        Content = $"Ты чё чудишь!",
                        Flags = MessageFlags.Ephemeral
                    };
                    await RespondAsync(InteractionCallback.Message(errorMsgProps2));
                    return;
                }
            }

            var newUserBet = new UserBet
            {
                Raffle = raffle,
                User = user,
                UserName = string.IsNullOrEmpty(user.Nickname) ? user.Username : user.Nickname,
                BetAmount = bet,
                AnswerNumber = answerNum,
                CreatedAt = DateTime.UtcNow.AddHours(3),
            };
            if (!dbContext.TryAddUserBet(newUserBet))
            {
                InteractionMessageProperties errorMsgProps = new()
                {
                    Content = "Внутренняя ошибка",
                    Flags = MessageFlags.Ephemeral
                };
                await RespondAsync(InteractionCallback.Message(errorMsgProps));
                return;
            }

            dbContext.ChangeUserIqPoints(user.PrimaryKey, -bet);

            await RespondAsync(InteractionCallback.DeferredModifyMessage);
            await Context.Client.Rest.ModifyMessageAsync(Context.Channel.Id, raffle.AnswerButtonsMessageId, RebuildMessage);

            void RebuildMessage(MessageOptions options)
            {
                var betsWithAnswer_1 = dbContext.GetUsersBetsByAnswerNum(raffle, 1);
                var betsWithAnswer_2 = dbContext.GetUsersBetsByAnswerNum(raffle, 2);
                var betsWithAnswer_3 = dbContext.GetUsersBetsByAnswerNum(raffle, 3);
                var betsWithAnswer_4 = dbContext.GetUsersBetsByAnswerNum(raffle, 4);

                ButtonProperties buttonEndRaffle = new($"{RaffleConstants.ButtonEndRaffleId}:{raffle.Id}", "Завершить", NetCord.ButtonStyle.Primary);
                ButtonProperties buttonAnswer_1 = new(RaffleConstants.ButtonAnswer_1_Id, "Ответ № 1", NetCord.ButtonStyle.Primary);
                ButtonProperties buttonAnswer_2 = new(RaffleConstants.ButtonAnswer_2_Id, "Ответ № 2", NetCord.ButtonStyle.Primary);
                ButtonProperties? buttonAnswer_3 = null;
                ButtonProperties? buttonAnswer_4 = null;

                EmbedFieldProperties? fieldAnswer_1 = null;
                EmbedFieldProperties? fieldAnswer_2 = null;
                EmbedFieldProperties? fieldAnswer_3 = null;
                EmbedFieldProperties? fieldAnswer_4 = null;

                fieldAnswer_1 = new() { Name = "Ответ № 1:", Value = $"{raffle.Answer_1}\n< Кол-во ставок: {betsWithAnswer_1.Length} | Всего вложено: {betsWithAnswer_1.Sum(ub => ub.BetAmount)} >" };
                fieldAnswer_2 = new() { Name = "Ответ № 2:", Value = $"{raffle.Answer_2}\n< Кол-во ставок: {betsWithAnswer_2.Length} | Всего вложено: {betsWithAnswer_2.Sum(ub => ub.BetAmount)} >" };

                if (!string.IsNullOrEmpty(raffle.Answer_3))
                {
                    buttonAnswer_3 = new(RaffleConstants.ButtonAnswer_3_Id, "Ответ № 3", NetCord.ButtonStyle.Primary);
                    fieldAnswer_3 = new() { Name = "Ответ № 3:", Value = $"{raffle.Answer_3}\n< Кол-во ставок: {betsWithAnswer_3.Length} | Всего вложено: {betsWithAnswer_3.Sum(ub => ub.BetAmount)} >" };
                }

                if (!string.IsNullOrEmpty(raffle.Answer_4))
                {
                    buttonAnswer_4 = new(RaffleConstants.ButtonAnswer_4_Id, "Ответ № 4", NetCord.ButtonStyle.Primary);
                    fieldAnswer_4 = new() { Name = "Ответ № 4:", Value = $"{raffle.Answer_4}\n< Кол-во ставок: {betsWithAnswer_4.Length} | Всего вложено: {betsWithAnswer_4.Sum(ub => ub.BetAmount)} >" };
                }

                ActionRowProperties actionRow = new()
                {
                    Components = [buttonAnswer_1, buttonAnswer_2],
                };

                if (buttonAnswer_3 != null)
                {
                    actionRow.AddComponents(buttonAnswer_3);
                }
                if (buttonAnswer_4 != null)
                {
                    actionRow.AddComponents(buttonAnswer_4);
                }

                actionRow.AddComponents(buttonEndRaffle);

                EmbedProperties embedProps = new()
                {
                    Title = raffle.Question,
                    Fields = [fieldAnswer_1!, fieldAnswer_2!],
                    Color = new(0xd90f3e)
                };
                if (fieldAnswer_3 != null)
                {
                    embedProps.AddFields(fieldAnswer_3);
                }
                if (fieldAnswer_4 != null)
                {
                    embedProps.AddFields(fieldAnswer_4);
                }

                options.Components = [actionRow];
                options.Embeds = [embedProps];
            }
        }

        //-------------------------------------------------------------HELPERS------------------------------------------------------------------------------------------------------

        private static void GetBetsStats(UserBet[] bets, int correctAnswer, out int totalLosersBets, out int totalWinnersBets)
        {
            totalLosersBets = 0;
            totalWinnersBets = 0;
            for (int i = 0; i < bets.Length; i++)
            {
                var bet = bets[i];
                if (bet == null) continue;

                if (bet.AnswerNumber == correctAnswer)
                {
                    totalWinnersBets += bet.BetAmount;
                }
                else
                {
                    totalLosersBets += bet.BetAmount;
                }
            }
        }

        private static int GetUserIQChange(UserBet userBet, int correctAnswerNum, int totalLosersBets, int totalWinnersBets, int additionalFixedReward)
        {
            if (correctAnswerNum != userBet.AnswerNumber)
            {
                return userBet.BetAmount * -1;
            }

            float winnerFraction = ((float)userBet.BetAmount) / ((float)totalWinnersBets);
            return (int)(userBet.BetAmount + (float)totalLosersBets * winnerFraction) + additionalFixedReward;
        }
    }
}
