using DiscordBotApi.Data.TelegramBotImages;
using DiscordBotApi.Database;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DiscordBotApi.TelegramBot;

public class TelegramBotService
{
    readonly TelegramBotClient _botClient;
    readonly ApplicationDbContext _dbContext;
    readonly TelegramBotSlashCommands _slashCommands;
    readonly static string _baseImagePath = Path.Combine(AppContext.BaseDirectory, "ImageStorage");

    public TelegramBotService(TelegramBotClient telegramBotClient, ApplicationDbContext dbContext, TelegramBotSlashCommands slashCommands)
    {
        _botClient = telegramBotClient;
        _dbContext = dbContext;
        _slashCommands = slashCommands;

        if (!Directory.Exists(_baseImagePath))
        {
            Directory.CreateDirectory(_baseImagePath);
        }
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { } rawText } message)
            return;

        if (message.From?.IsBot == true)
            return;

        //Commands
        if (rawText.StartsWith('/'))
        {
            var words = rawText.Split(' ', '@');
            if (words is not null && words.Length > 0)
            {
                var command = words[0].ToLowerInvariant();
                await _slashCommands.ExecuteCommand(command, update, cancellationToken);
            }

            return;
        }

        //Text to image replace
        if (!TelegramBotHelpers.TryNormalizeWord(rawText, out var normalized))
            return;

        var record = _dbContext.TelegramBotImages
            .FirstOrDefault(w => w.Command == normalized);

        if (record is null)
            return;

        var fullPath = Path.Combine(_baseImagePath, record.ImageFileName);
        if (!File.Exists(fullPath))
        {
            Log.Warning("Для команды '{0}' не найден файл {1}", record.Command, fullPath);
            return;
        }

        ReplyParameters? replyParams = message.ReplyToMessage is { } repliedTo
            ? new ReplyParameters { MessageId = repliedTo.Id }
            : null;

        try
        {
            if (IsAnimated(record.ImageFileName))
                await SendAnimationWithReplyAsync(_botClient, _dbContext, message.Chat.Id, message.From, replyParams, record, fullPath, cancellationToken);
            else
                await SendImageWithReplyAsync(_botClient, _dbContext, message.Chat.Id, message.From, replyParams, record, fullPath, cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 400 && replyParams is not null)
        {
            Log.Warning(ex,"Не удалось ответить на сообщение, отправляем без reply");

            if (IsAnimated(record.ImageFileName))
                await SendAnimationWithReplyAsync(_botClient, _dbContext, message.Chat.Id, message.From, null, record, fullPath, cancellationToken);
            else
                await SendImageWithReplyAsync(_botClient, _dbContext, message.Chat.Id, message.From, null, record, fullPath, cancellationToken);
        }

        try
        {
            await _botClient.DeleteMessage(message.Chat.Id, message.Id, cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 403)
        {
            Log.Warning(
                "Нет прав на удаление сообщений в чате {Chat}. " +
                "Проверьте, что бот — администратор.", message.Chat.Id);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 400)
        {
            Log.Warning("Не удалось удалить сообщение: {Message}", ex.Message);
        }
    }

    public async Task SetWebhookAsync(string webhookUrl)
    {
        var secretToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_SECRET");
        if (string.IsNullOrWhiteSpace(secretToken))
            return;

        await _botClient.SetWebhook(
            url: webhookUrl,
            secretToken: secretToken
        );

        Log.Information("Вебхук успешно зарегистрирован на {Url}", webhookUrl);
    }

    private static async Task SendImageWithReplyAsync(
        TelegramBotClient botClient,
        ApplicationDbContext context,
        long chatId,
        User? senderUser,
        ReplyParameters? rp,
        TelegramBotImage record,
        string fullPath,
        CancellationToken ct)
    {
        string caption;
        string username;
        if (senderUser != null)
        {
            username = senderUser.Username ?? senderUser.FirstName;
            caption = $"<a href=\"tg://user?id={senderUser.Id}\">{username}</a>";
        }
        else
        {
            caption = $"кто-то ¯\\_(ツ)_/¯";
        }

        if (string.IsNullOrWhiteSpace(record.TelegramFileId))
        {
            //open stream, send file
            await using var stream = File.OpenRead(fullPath);
            var sentMessage = await botClient.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromStream(stream, record.ImageFileName),
                    replyParameters: rp,
                    caption: caption,
                    parseMode: ParseMode.Html,
                    disableNotification: true,
                    cancellationToken: ct);

            //save image id
            if (sentMessage.Photo?.Length > 0)
            {
                record.TelegramFileId = sentMessage.Photo.Last().FileId;
                context.SaveChanges();
            }

            return;
        }

        try
        {
            await botClient.SendPhoto(
                        chatId: chatId,
                        photo: record.TelegramFileId,
                        replyParameters: rp,
                        caption: caption,
                        parseMode: ParseMode.Html,
                        disableNotification: true,
                        cancellationToken: ct);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 400)
        {
            Log.Warning("File_id {Id} is stale, re-uploading...", record.TelegramFileId);

            //reupload image and save new FieldId
            await using var stream = File.OpenRead(fullPath);
            var sentMessage = await botClient.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromStream(stream, record.ImageFileName),
                    replyParameters: rp,
                    caption: caption,
                    parseMode: ParseMode.Html,
                    disableNotification: true,
                    cancellationToken: ct);

            //save image id
            if (sentMessage.Photo?.Length > 0)
            {
                record.TelegramFileId = sentMessage.Photo.Last().FileId;
                context.SaveChanges();
            }
        }
    }

    private static async Task SendAnimationWithReplyAsync(
        TelegramBotClient botClient,
        ApplicationDbContext context,
        long chatId,
        User? senderUser,
        ReplyParameters? rp,
        TelegramBotImage record,
        string fullPath,
        CancellationToken ct)
    {
        string caption;
        string username;
        if (senderUser != null)
        {
            username = senderUser.Username ?? senderUser.FirstName;
            caption = $"<a href=\"tg://user?id={senderUser.Id}\">{username}</a>";
        }
        else
        {
            caption = $"кто-то ¯\\_(ツ)_/¯";
        }

        if (string.IsNullOrWhiteSpace(record.TelegramFileId))
        {
            //open stream, send file
            await using var stream = File.OpenRead(fullPath);
            var sentMessage = await botClient.SendAnimation(
                chatId: chatId,
                animation: InputFile.FromStream(stream, record.ImageFileName),
                replyParameters: rp,
                caption: caption,
                parseMode: ParseMode.Html,
                disableNotification: true,
                cancellationToken: ct);

            //save image id
            if (sentMessage.Photo?.Length > 0)
            {
                record.TelegramFileId = sentMessage.Animation?.FileId;
                context.SaveChanges();
            }

            return;
        }

        try
        {
            await botClient.SendAnimation(
                    chatId: chatId,
                    animation: record.TelegramFileId,
                    replyParameters: rp,
                    caption: caption,
                    parseMode: ParseMode.Html,
                    disableNotification: true,
                    cancellationToken: ct);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 400)
        {
            Log.Warning("File_id {Id} is stale, re-uploading...", record.TelegramFileId);

            //reupload image and save new FieldId
            await using var stream = File.OpenRead(fullPath);
            var sentMessage = await botClient.SendAnimation(
                    chatId: chatId,
                    animation: InputFile.FromStream(stream, record.ImageFileName),
                    replyParameters: rp,
                    caption: caption,
                    parseMode: ParseMode.Html,
                    disableNotification: true,
                    cancellationToken: ct);

            //save image id
            if (sentMessage.Photo?.Length > 0)
            {
                record.TelegramFileId = sentMessage.Animation?.FileId;
                context.SaveChanges();
            }
        }
    }

    static bool IsAnimated(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() is ".gif" or ".webp";
}