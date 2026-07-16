using DiscordBotApi.Data.Raffles;
using DiscordBotApi.Database;

namespace DiscordBotApi.DiscordBot.BotFeatures.RaffleService
{
    public static class RaffleUtils
    {
        public static void CloseRaffle(IServiceProvider provider, ulong messageIs)
        {
            using var scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            if (dbContext == null)
                return;

            var raffle = dbContext.Rafles.FirstOrDefault(r => r.AnswerButtonsMessageId == messageIs);
            if (raffle == null)
                return;

            raffle.IsClosed = true;

            var userBets = dbContext.UserBets.Where(ub => ub.Raffle.Id == raffle.Id).Select(ub => new UserBet
            {
                Id = ub.Id,
                User = ub.User,
                BetAmount = ub.BetAmount,
            }).ToArray();
            if (userBets.Length == 0)
            {
                dbContext.SaveChanges();
                return;
            }

            foreach (var bet in userBets)
            {
                bet.User.UserSpendingResource += bet.BetAmount;
            }

            dbContext.SaveChanges();
        }
    }
}
