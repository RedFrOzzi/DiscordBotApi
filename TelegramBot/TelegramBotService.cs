using DiscordBotApi.Database;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace DiscordBotApi.TelegramBot;

public class TelegramBotService
{
    readonly TelegramBotClient _botClient;
    readonly ApplicationDbContext _dbContext;
    readonly static string _baseImagePath = Path.Combine(AppContext.BaseDirectory, "ImageStorage");

    public TelegramBotService(TelegramBotClient telegramBotClient, ApplicationDbContext dbContext)
    {
        _botClient = telegramBotClient;
        _dbContext = dbContext;

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

        if (!TelegramBotHelpers.TryNormalizeWord(rawText, out var normalized))
            return;

        var record = _dbContext.TelegramBotImages
            .AsNoTracking()
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
            await SendImageWithReplyAsync(_botClient, message.Chat.Id, replyParams, fullPath, record.Command, cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 400 && replyParams is not null)
        {
            Log.Warning(ex,"Не удалось ответить на сообщение, отправляем без reply");
            await SendImageWithReplyAsync(_botClient, message.Chat.Id, null, fullPath, record.Command, cancellationToken);
        }

        try
        {
            await using var stream = File.OpenRead(fullPath);
            await _botClient.SendPhoto(
                chatId: message.Chat.Id,
                photo: InputFile.FromStream(stream, record.ImageFileName),
                replyParameters: replyParams,
                cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex)
        {
            Log.Error(ex, "Не удалось отправить картинку для '{0}'", record.Command);
            return;
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
        long chatId,
        ReplyParameters? rp,
        string fullPath,
        string imageName,
        CancellationToken ct)
    {
        await using var stream = File.OpenRead(fullPath);
        await botClient.SendPhoto(
            chatId: chatId,
            photo: InputFile.FromStream(stream, imageName),
            replyParameters: rp,
            cancellationToken: ct);
    }
}