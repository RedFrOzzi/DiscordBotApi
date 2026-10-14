using NetCord.Services.ApplicationCommands;

namespace DiscordBotApi.Utilities;

public enum ButtonsColorTheme
{
    [SlashCommandChoice(Name = "Стандарт")]
    Standard = 0,

    [SlashCommandChoice(Name = "Чередующиеся")]
    Alternates = 1,

    [SlashCommandChoice(Name = "Радуга")]
    FullColors = 2,

    [SlashCommandChoice(Name = "Хаос")]
    Chaos = 3
}
