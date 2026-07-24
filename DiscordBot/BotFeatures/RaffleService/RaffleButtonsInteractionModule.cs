using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities.Result;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace DiscordBotApi.DiscordBot.BotFeatures.RaffleService;

public class RaffleButtonsInteractionModule(ApplicationDbContext dbContext) 
    : ComponentInteractionModule<ButtonInteractionContext>
{
    readonly ApplicationDbContext _dbContext = dbContext;

    [ComponentInteraction(RaffleConstants.ButtonEndRaffleId)]
    public async Task ButtonCloseRaffle(int raffleId)
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

        var checkbox = new CheckboxProperties("deleteraffletoggle")
        {
            Default = false
        };

        ModalProperties mProps = new($"{RaffleConstants.ModalCorrectAnswerSelectionId}:{raffleId}", "Правильный ответ");
        mProps.AddComponents(
            new LabelProperties("Выбор", stringMenu),
            new LabelProperties("Удалить текущую игру и вернуть ресурсы", checkbox));

        var callback = InteractionCallback.Modal(mProps);
        await RespondAsync(callback);
    }

    [ComponentInteraction(RaffleConstants.ButtonAnswer_1_Id)]
    public async Task ButtonAnswer_1() => await SendAnswerModal(1);

    [ComponentInteraction(RaffleConstants.ButtonAnswer_2_Id)]
    public async Task ButtonAnswer_2() => await SendAnswerModal(2);

    [ComponentInteraction(RaffleConstants.ButtonAnswer_3_Id)]
    public async Task ButtonAnswer_3() => await SendAnswerModal(3);

    [ComponentInteraction(RaffleConstants.ButtonAnswer_4_Id)]
    public async Task ButtonAnswer_4() => await SendAnswerModal(4);

    private async Task SendAnswerModal(int answerNum)
    {
        var userInDb = _dbContext.DiscordUsers
            .AsNoTracking()
            .FirstOrDefault(u => u.Id == Context.User.Id);

        if (userInDb == null)
        {
            InteractionMessageProperties errorMsgProps = new()
            {
                Content = "Тебя нет в базе данных",
                Flags = MessageFlags.Ephemeral
            };
            await RespondAsync(InteractionCallback.Message(errorMsgProps));
            return;
        }

        //Get raffle with answer buttons message id
        var raffle = _dbContext.Rafles
            .AsNoTracking()
            .FirstOrDefault(r => r.AnswerButtonsMessageId == Context.Message.Id);

        if (raffle == null)
        {
            InteractionMessageProperties errorMsgProps = new()
            {
                Content = "Игра для этого сообщения не найдена",
                Flags = MessageFlags.Ephemeral
            };
            await RespondAsync(InteractionCallback.Message(errorMsgProps));
            return;
        }

        if (_dbContext.UserBets.Any(b => b.Raffle.Id == raffle.Id && b.User.Id == Context.User.Id))
        {
            InteractionMessageProperties errorMsgProps = new()
            {
                Content = "Уже делал ставку!",
                Flags = MessageFlags.Ephemeral
            };
            await RespondAsync(InteractionCallback.Message(errorMsgProps));
            return;
        }

        var maxBet = userInDb.UserSpendingResource > 0 ? userInDb.UserSpendingResource : 0;
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
    }
}
