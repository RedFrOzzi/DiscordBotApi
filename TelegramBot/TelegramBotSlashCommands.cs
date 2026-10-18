using DiscordBotApi.Database;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiscordBotApi.TelegramBot;

public record TGBotCommand(string Command, string Description, Func<Update, CancellationToken, Task> Function);

public class TelegramBotSlashCommands
{
    public Dictionary<string, TGBotCommand> CommandsMap { get; }

    readonly ApplicationDbContext _dbContext;
    readonly TelegramBotClient _botClient;

    public TelegramBotSlashCommands(ApplicationDbContext dbContext, TelegramBotClient botClient)
    {
        _dbContext = dbContext;
        _botClient = botClient;

        CommandsMap = new Dictionary<string, TGBotCommand>
        {
            { "info",  new("info",  "Информация",     InfoCommand) },
            { "words", new("words", "Слова-триггеры", WordsCommand) },
        };
    }

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

    public async Task WordsCommand(Update update, CancellationToken ct)
    {
        if (update is null || update.Message is null || update.Message.Chat is null)
            return;

        var records = _dbContext.TelegramBotImages.ToList();
        if (records is null || records.Count == 0)
            return;

        StringBuilder sb = new();
        foreach (var item in records)
        {
            sb.AppendLine(item.Command);
        }

        await _botClient.SendMessage(
            update.Message.Chat.Id,
            sb.ToString(),
            replyParameters: new() { MessageId = update.Message.Id },
            disableNotification: true,
            cancellationToken: ct);
    }
}
