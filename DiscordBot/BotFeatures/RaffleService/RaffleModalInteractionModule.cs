using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Database;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace DiscordBotApi.DiscordBot.BotFeatures.RaffleService;

public class RaffleModalInteractionModule(ApplicationDbContext dbContext) : ComponentInteractionModule<ModalInteractionContext>
{
    readonly ApplicationDbContext _dbContext = dbContext;

    //Respond to game creation command
    [ComponentInteraction(RaffleConstants.RaffleInitialModalId)]
    public async Task RespondToRaffleModal()
    {
        await RespondAsync(InteractionCallback.DeferredModifyMessage);

        if (Context == null || Context.Components == null || Context.Components.Count == 0 || Context.Guild == null)
        {
            await FollowupAsync(new() { Content = "Внутренняя ошибка", Flags = MessageFlags.Ephemeral });
            return;
        }

        var guild = await _dbContext.Guilds.FirstOrDefaultAsync(g => g.Id == Context.Guild.Id);

        if (guild == null)
        {
            await FollowupAsync(new() { Content = "Канал не найден", Flags = MessageFlags.Ephemeral });
            return;
        }

        var newRaffle = new Raffle()
        {
            Guild = guild
        };

        _dbContext.Rafles.Add(newRaffle);

        if (_dbContext.SaveChanges() == 0)
        {
            await FollowupAsync(new() { Content = "Ошибка создания", Flags = MessageFlags.Ephemeral });
            return;
        }

        newRaffle.CreatedAt = DateTime.UtcNow.AddHours(3);

        ButtonProperties buttonEndRaffle = new($"{RaffleConstants.ButtonEndRaffleId}:{newRaffle.Id}", "Завершить", NetCord.ButtonStyle.Primary);
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
                newRaffle.Question = input.Value ?? "";
                continue;
            }

            if (input.CustomId == RaffleConstants.ModalAnswer_1_Id)
            {
                newRaffle.Answer_1 = input.Value ?? "";
                fieldAnswer_1 = new() { Name = "Ответ № 1:", Value = $"{newRaffle.Answer_1}\n< Кол-во ставок: 0 | Всего вложено: 0 >" };
                continue;
            }

            if (input.CustomId == RaffleConstants.ModalAnswer_2_Id)
            {
                newRaffle.Answer_2 = input.Value ?? "";
                fieldAnswer_2 = new() { Name = "Ответ № 2:", Value = $"{newRaffle.Answer_2}\n< Кол-во ставок: 0 | Всего вложено: 0 >" };
                continue;
            }

            if (input.CustomId == RaffleConstants.ModalAnswer_3_Id)
            {
                if (string.IsNullOrEmpty(input.Value))
                {
                    newRaffle.Answer_3 = string.Empty;
                    continue;
                }

                newRaffle.Answer_3 = input.Value;
                buttonAnswer_3 = new(RaffleConstants.ButtonAnswer_3_Id, "Ответ № 3", NetCord.ButtonStyle.Primary);
                fieldAnswer_3 = new() { Name = "Ответ № 3:", Value = $"{newRaffle.Answer_3}\n< Кол-во ставок: 0 | Всего вложено: 0 >" };
                continue;
            }

            if (input.CustomId == RaffleConstants.ModalAnswer_4_Id)
            {
                if (string.IsNullOrEmpty(input.Value))
                {
                    newRaffle.Answer_4 = string.Empty;
                    continue;
                }

                newRaffle.Answer_4 = input.Value;
                buttonAnswer_4 = new(RaffleConstants.ButtonAnswer_4_Id, "Ответ № 4", NetCord.ButtonStyle.Primary);
                fieldAnswer_4 = new() { Name = "Ответ № 4:", Value = $"{newRaffle.Answer_4}\n< Кол-во ставок: 0 | Всего вложено: 0 >" };
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
            Title = newRaffle.Question,
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
        var message = await FollowupAsync(props);

        if (message == null || message.Id == 0)
        {
            await FollowupAsync(new() { Content = "Ошибка создания", Flags = MessageFlags.Ephemeral });
            return;
        }

        newRaffle.AnswerButtonsMessageId = message.Id;
        newRaffle.IsClosed = false;

        if (_dbContext.SaveChanges() == 0)
        {
            await FollowupAsync(new() { Content = "Ошибка записи на диск", Flags = MessageFlags.Ephemeral });
            return;
        }
    }

    //Respond to correct answer selection
    [ComponentInteraction(RaffleConstants.ModalCorrectAnswerSelectionId)]
    public async Task RespondToAnswerNumberSelection(int raffleId)
    {
        await RespondAsync(InteractionCallback.DeferredModifyMessage);

        bool? deleteRaffle = Context.Components
            ?.OfType<Label>()
            ?.Select(l => l.Component)
            ?.OfType<Checkbox>()
            ?.FirstOrDefault()
            ?.Checked;

        if (deleteRaffle != null && deleteRaffle == true)
        {
            var r = _dbContext.Rafles
            .FirstOrDefault(r => r.Id == raffleId);

            if (r == null || r.IsClosed)
            {
                await FollowupAsync(new() { Content = "Эта игра уже закрыта или её нет", Flags = MessageFlags.Ephemeral });
                return;
            }

            var bets = _dbContext.UserBets
            .Where(ub => ub.Raffle.Id == r.Id)
            .Select(ub => new UserBet
            {
                Id = ub.Id,
                User = ub.User,
                AnswerNumber = ub.AnswerNumber,
                BetAmount = ub.BetAmount,
                Raffle = ub.Raffle
            });

            foreach (var b in bets)
            {
                b.User.UserSpendingResource += b.BetAmount;
            }

            r.IsClosed = true;

            _dbContext.SaveChanges();

            //Delete message with buttons from discord
            if (r.AnswerButtonsMessageId != 0)
            {
                await Context.Client.Rest.DeleteMessageAsync(Context.Channel.Id, r.AnswerButtonsMessageId);
            }

            await FollowupAsync(new() { Content = "Готово", Flags = MessageFlags.Ephemeral });
            return;
        }

        var values = Context.Components
            ?.OfType<Label>()
            ?.Select(l => l.Component)
            ?.OfType<StringMenu>()
            ?.FirstOrDefault()
            ?.SelectedValues;

        int answerNum = -1;
        if (values != null && values.Count > 0 && int.TryParse(values[0], out var res))
        {
            answerNum = res;
        }

        if (answerNum < 0)
        {
            await FollowupAsync(new() { Content = "Ошибка выбора ответа", Flags = MessageFlags.Ephemeral });
            return;
        }

        var raffle = _dbContext.Rafles
            .FirstOrDefault(r => r.Id == raffleId);

        if (raffle == null || raffle.IsClosed)
        {
            await FollowupAsync(new() { Content = "Эта игра уже закрыта или её нет", Flags = MessageFlags.Ephemeral });
            return;
        }

        var userBets = _dbContext.UserBets
            .Where(ub => ub.Raffle.Id == raffle.Id)
            .Select(ub => new UserBet
                {
                    Id = ub.Id,
                    User = ub.User,
                    AnswerNumber = ub.AnswerNumber,
                    BetAmount = ub.BetAmount,
                    Raffle = ub.Raffle
                })
            .ToArray();

        if (userBets.Length <= 1)
        {
            await FollowupAsync(new() { Content = "Ошибка, в игре учавствует только один игрок", Flags = MessageFlags.Ephemeral });
            return;
        }

        GetBetsStats(userBets, answerNum, out var totalLosersBets, out var totalWinnersBets);

        for (int i = 0; i < userBets.Length; i++)
        {
            var discordUser = userBets[i].User;
            if (discordUser == null) { continue; }

            int resourceChange = GetUserResourceChange(userBets[i], answerNum, totalLosersBets, totalWinnersBets, RaffleConstants.AdditionalIQOnWin);
            userBets[i].User.UserSpendingResource += resourceChange;
        }

        raffle.IsClosed = true;

        _dbContext.SaveChanges();

        //Delete message with buttons from discord
        if (raffle.AnswerButtonsMessageId != 0)
        {
            await Context.Client.Rest.DeleteMessageAsync(Context.Channel.Id, raffle.AnswerButtonsMessageId);
        }

        await FollowupAsync(new() { Content = "Готово", Flags = MessageFlags.Ephemeral });
        return;
    }

    //Respond to user bet amount
    [ComponentInteraction(RaffleConstants.BetAmountModalId)]
    public async Task RespondToAnswerGiven(int answerNum, int maxUserBetAmount, int raffleId)
    {
        await RespondAsync(InteractionCallback.DeferredModifyMessage);

        var raffle = _dbContext.Rafles.FirstOrDefault(r => r.Id == raffleId);
        if (Context == null || Context.Components == null || Context.Components.Count == 0 || raffle == null)
        {
            await FollowupAsync(new() { Content = "Внутренняя ошибка", Flags = MessageFlags.Ephemeral });
            return;
        }

        var user = _dbContext.DiscordUsers
            .FirstOrDefault(u => u.Id == Context.User.Id);

        if (user == null)
        {
            await FollowupAsync(new() { Content = "Тебя нет в базе данных", Flags = MessageFlags.Ephemeral });
            return;
        }

        int bet = 0;
        foreach (var component in Context.Components)
        {
            if (component is not Label label || label.Component is not TextInput input) { continue; }

            if (!int.TryParse(input.Value, out bet) || bet <= 0 || bet > maxUserBetAmount)
            {
                await FollowupAsync(new() { Content = "Ты чё чудишь!", Flags = MessageFlags.Ephemeral });
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

        _dbContext.UserBets.Add(newUserBet);
        var isAdded = _dbContext.SaveChanges() > 0;

        if (!isAdded)
        {
            await FollowupAsync(new() { Content = "Внутренняя ошибка", Flags = MessageFlags.Ephemeral });
            return;
        }

        user.UserSpendingResource -= bet;
        _dbContext.SaveChanges();
        
        await Context.Client.Rest.ModifyMessageAsync(Context.Channel.Id, raffle.AnswerButtonsMessageId, RebuildMessage);

        void RebuildMessage(MessageOptions options)
        {
            var betsWithAnswer_1 = GetUsersBetsByAnswerNum(_dbContext, raffle, 1);
            var betsWithAnswer_2 = GetUsersBetsByAnswerNum(_dbContext, raffle, 2);
            var betsWithAnswer_3 = GetUsersBetsByAnswerNum(_dbContext, raffle, 3);
            var betsWithAnswer_4 = GetUsersBetsByAnswerNum(_dbContext, raffle, 4);

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

    private static int GetUserResourceChange(UserBet userBet, int correctAnswerNum, int totalLosersBets, int totalWinnersBets, int additionalFixedReward)
    {
        if (correctAnswerNum != userBet.AnswerNumber)
        {
            return userBet.BetAmount * -1;
        }

        float winnerFraction = ((float)userBet.BetAmount) / ((float)totalWinnersBets);
        return (int)(userBet.BetAmount + (float)totalLosersBets * winnerFraction) + additionalFixedReward;
    }

    private static UserBet[] GetUsersBetsByAnswerNum(ApplicationDbContext ctx, Raffle raffle, int answerNum)
    {
        return ctx.UserBets
            .AsNoTracking()
            .Where(ub => ub.Raffle.Id == raffle.Id && ub.AnswerNumber == answerNum)
            .Select(ub => new UserBet
            {
                Id = ub.Id,
                User = ub.User,
                AnswerNumber = ub.AnswerNumber,
                BetAmount = ub.BetAmount,
                Raffle = ub.Raffle
            })
            .ToArray();
    }
}
