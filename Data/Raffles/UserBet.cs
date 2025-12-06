using DiscordBotApi.Data.Users;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.Raffles
{
    public class UserBet
    {
        [Key]
        public int Id { get; set; }
        public Raffle Raffle { get; set; } = null!;
        public DiscordUser User { get; set; } = null!;
        public string UserName { get; set; } = string.Empty;
        public int BetAmount { get; set; }
        public int AnswerNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
