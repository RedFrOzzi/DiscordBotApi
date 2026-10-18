using DiscordBotApi.Database;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiscordBotApi.TelegramBot;

public record TGBotCommand(string Command, string Description, Func<Update, CancellationToken, Task> Function);

public class TelegramBotSlashCommands(ApplicationDbContext dbContext, TelegramBotClient botClient)
{
    public Dictionary<string, TGBotCommand> CommandsMap => new()
    {
        { "info", new("info", "Информация", InfoCommand)}
    };

    readonly ApplicationDbContext _dbContext = dbContext;
    readonly TelegramBotClient _botClient = botClient;

    public async Task SetBotCommands()
    {
        if (CommandsMap.Count == 0)
            return;

        List<BotCommand> commands = new(CommandsMap.Count);
        foreach (var values in CommandsMap.Values)
        {
            commands.Add(
                new(values.Command, values.Description));
        }

        await _botClient.SetMyCommands(commands);
    }

    public async Task ExecuteCommand(string command, Update update, CancellationToken ct)
    {
        if (CommandsMap.TryGetValue(command, out var tgCommand))
        {
            await tgCommand.Function(update, ct);
        }
    }

    public async Task InfoCommand(Update update, CancellationToken ct)
    {
        if (update is null || update.Message is null || update.Message.Chat is null)
            return;

        await _botClient.SendMessage(
            update.Message.Chat.Id,
            "Бот заменяет текст на изображения/анимацию.",
            replyParameters: new() { MessageId = update.Message.Id },
            disableNotification: true,
            cancellationToken: ct);
    }
}
