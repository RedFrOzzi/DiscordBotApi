using NetCord.Services.ApplicationCommands;

namespace DiscordBotApi.Utilities
{
    public enum AnswerNumber
    {
        [SlashCommandChoice(Name = "Ответ номер один (1)")]
        One = 1,
        [SlashCommandChoice(Name = "Ответ номер два (2)")]
        Two = 2,
        [SlashCommandChoice(Name = "Ответ номер три (3)")]
        Three = 3,
        [SlashCommandChoice(Name = "Ответ номер четыре (4)")]
        Four = 4,
    }
}
