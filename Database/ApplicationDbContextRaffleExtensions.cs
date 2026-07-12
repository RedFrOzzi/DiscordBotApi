using DiscordBotApi.Controllers;
using DiscordBotApi.Data.DiscordUsers;
using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Data.Roles;
using Microsoft.EntityFrameworkCore;
using NetCord.Services;

namespace DiscordBotApi.Database;

public static class ApplicationDbContextRaffleExtensions
{
    public static bool TryCreateNewRaffle(this ApplicationDbContext ctx, Raffle raffle, out int insertedRaffleId)
    {
        ctx.Rafles.Add(raffle);
        bool isInserted = ctx.SaveChanges() > 0;
        insertedRaffleId = raffle.Id;
        return isInserted;
    }

    public static bool TryUpdateRaffle(this ApplicationDbContext ctx, int raffleId, Raffle newValuesRaffle)
    {
        var foundRaffle = ctx.Rafles.FirstOrDefault(r => r.Id == raffleId);
        if (foundRaffle == null)
        {
            return false;
        }

        foundRaffle.Question = newValuesRaffle.Question;
        foundRaffle.Answer_1 = newValuesRaffle.Answer_1;
        foundRaffle.Answer_2 = newValuesRaffle.Answer_2;
        foundRaffle.Answer_3 = newValuesRaffle.Answer_3;
        foundRaffle.Answer_4 = newValuesRaffle.Answer_4;
        foundRaffle.AnswerButtonsMessageId = newValuesRaffle.AnswerButtonsMessageId;
        foundRaffle.IsClosed = newValuesRaffle.IsClosed;
        foundRaffle.CreatedAt = newValuesRaffle.CreatedAt;

        return ctx.SaveChanges() > 0;
    }

    public static Raffle? GetRaffle(this ApplicationDbContext ctx, int raffleId)
    {
        return ctx.Rafles
            .AsNoTracking()
            .FirstOrDefault(r => r.Id == raffleId);
    }

    public static Raffle? GetRaffleWithAnswerButtonsMessageId(this ApplicationDbContext ctx, ulong answerButtonsMessageId)
    {
        return ctx.Rafles
            .AsNoTracking()
            .FirstOrDefault(r => r.AnswerButtonsMessageId == answerButtonsMessageId);
    }

    public static UserBet[] GetUsersBets(this ApplicationDbContext ctx, Raffle raffle)
    {
        return ctx.UserBets
            .AsNoTracking()
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
    }

    public static UserBet[] GetUserBets(this ApplicationDbContext ctx, ulong userId)
    {
        return ctx.UserBets
            .AsNoTracking()
            .Where(ub => ub.User.Id == userId)
            .Select(ub => new UserBet
                {
                    Id = ub.Id,
                    AnswerNumber = ub.AnswerNumber,
                    BetAmount = ub.BetAmount,
                    CreatedAt = ub.CreatedAt,
                })
            .ToArray();
    }

    public static UserBet[] GetUsersBetsByAnswerNum(this ApplicationDbContext ctx, Raffle raffle, int answerNum)
    {
        return ctx.UserBets
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

    public static DiscordUser[] GetUsersWithBets(this ApplicationDbContext ctx, int raffleId)
    {
        return ctx.UserBets
            .AsNoTracking()
            .Where(ub => ub.Raffle.Id == raffleId)
            .Select(ub => ub.User)
            .ToArray();
    }

    public static bool IsUserAlreadyBetInThisGame(this ApplicationDbContext ctx, int raffleId, ulong userId)
    {
        return ctx.UserBets
            .AsNoTracking()
            .Any(b => b.Raffle.Id == raffleId && b.User.Id == userId);
    }

    public static bool TryAddUserBet(this ApplicationDbContext ctx, UserBet bet)
    {
        ctx.UserBets.Add(bet);
        return ctx.SaveChanges() > 0;
    }

    public static void ChangeUserResourcePoints(this ApplicationDbContext ctx, int userKey, int iqPoints)
    {
        var user = ctx.DiscordUsers.Find(userKey);
        if (user == null)
        {
            return;
        }

        user.UserSpendingResource += iqPoints;
        ctx.SaveChanges();
    }

    public static ulong[]? GetAllowedRoleIds(this ApplicationDbContext ctx, ulong guildId)
    {
        return ctx.RaffleSettings
            .AsNoTracking()
            .FirstOrDefault(rs => rs.Guild.Id == guildId)
            ?.AllowedRoles
            .Select(ar => ar.Id)
            .ToArray();
    }
}
