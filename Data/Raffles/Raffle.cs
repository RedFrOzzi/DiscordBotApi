using DiscordBotApi.Data.Users;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.Raffles
{
    public class Raffle
    {
        [Key]
        public int Id { get; set; }
        public bool IsClosed { get; set; }
        public ulong AnswerButtonsMessageId { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer_1 { get; set; } = string.Empty;
        public string Answer_2 { get; set; } = string.Empty;
        public string Answer_3 { get; set; } = string.Empty;
        public string Answer_4 { get; set; } = string.Empty;
        //test changes
        public DateTime CreatedAt { get; set; }
    }
}
