using DiscordBotApi.Data.AudioPanels;
using DiscordBotApi.Data.AudioTracks;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities;
using DiscordBotApi.Utilities.Result;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway.Voice;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;

[SlashCommand("голос", "Команды, связанные с аудио каналами")]
public class VoceConnectionSlashCommandsModule(VoiceInstancesContainer voiceInstancesContainer,
    ApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    AsyncTimersCollection timers) 
    : ApplicationCommandModule<ApplicationCommandContext>
{
    readonly VoiceInstancesContainer _viContainer = voiceInstancesContainer;
    readonly ApplicationDbContext _dbContext = dbContext;
    readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    readonly AsyncTimersCollection _timers = timers;

    [SubSlashCommand("присоединиться", "Присоединяется к голосовому каналу")]
    public async Task ConnectToChannel()
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (Context.Guild is not { } guild)
        {
            await ModifyResponseAsync(m => m.Content = "Канал не найден");
            return;
        }

        var user = Context.User;
        if (await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext) is Error)
        {
            await ModifyResponseAsync(m => m.Content = "У тебя нет прав доступа");
            return;
        }

        ulong channelId;
        if (guild.VoiceStates.TryGetValue(user.Id, out var voiceState))
            channelId = voiceState.ChannelId.GetValueOrDefault();
        else
        {
            await ModifyResponseAsync(m => m.Content = "Ты должен находиться в голосовм канале");
            return;
        }

        var guildId = guild.Id;

        if (!_viContainer.VoiceInstances.TryAdd(guildId, null))
        {
            await ModifyResponseAsync(m => m.Content = "Бот уже находится в голосовом канале");
            return;
        }

        VoiceClient? voiceClient;
        try
        {
            voiceClient = await Context.Client.JoinVoiceChannelAsync(guildId, channelId, new());
        }
        catch
        {
            _viContainer.VoiceInstances.TryRemove(item: new(guildId, null));

            await Context.Client.UpdateVoiceStateAsync(new(guildId, null));

            throw;
        }

        VoiceInstance voiceInstance = new(voiceClient);

        if (!_viContainer.VoiceInstances.TryUpdate(guildId, voiceInstance, null))
        {
            voiceInstance.Dispose();

            await Context.Client.UpdateVoiceStateAsync(new(guildId, null));

            await ModifyResponseAsync(m => m.Content = "Не удалось зарегистрировать соединение");
            return;
        }

        voiceClient.Disconnect += args =>
        {
            if (args.Reconnect)
                return default;

            if (_viContainer.VoiceInstances.TryRemove(item: new(guildId, voiceInstance)))
                voiceInstance.Dispose();

            return default;
        };

        try
        {
            await voiceClient.StartAsync();
        }
        catch
        {
            if (_viContainer.VoiceInstances.TryRemove(item: new(guildId, voiceInstance)))
            {
                voiceInstance.Dispose();

                await Context.Client.UpdateVoiceStateAsync(new(guildId, null));
            }

            throw;
        }

        _timers.CreateOrResetTimer(guildId, TryDisconnectTheBot);

        await ModifyResponseAsync(m => m.Content = "Готово");
    }

    [SubSlashCommand("покинуть", "Покинуть голосовой канал")]
    public async Task LeaveChannel()
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (Context.Guild is not { } guild)
        {
            await ModifyResponseAsync(m => m.Content = "Канал не найден");
            return;
        }

        if (await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext) is Error)
        {
            await ModifyResponseAsync(m => m.Content = "У тебя нет прав доступа");
            return;
        }

        var guildId = guild.Id;

        if (!_viContainer.VoiceInstances.TryGetValue(guildId, out var voiceInstance) || voiceInstance is null)
        {
            await ModifyResponseAsync(m => m.Content = "Бот не подключен к голосовому каналу");
            return;
        }

        if (_viContainer.VoiceInstances.TryRemove(item: new(guildId, voiceInstance)))
        {
            try
            {
                await voiceInstance.Client.CloseAsync();
            }
            finally
            {
                voiceInstance.Dispose();

                await Context.Client.UpdateVoiceStateAsync(new(guildId, null));
            }
        }

        _timers.StopTheTimer(guildId);

        await ModifyResponseAsync(m => m.Content = "Бот покинул голосовой канал");
    }

    [SubSlashCommand("создать_аудио_панель", "Создает аудио панель в этом канале")]
    public async Task CreateAudioPanel()
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (Context.Guild is not { } guild || Context.Channel is not { } channel)
        {
            await ModifyResponseAsync(m => m.Content = "Канал не найден");
            return;
        }

        if (await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext) is Error)
        {
            await ModifyResponseAsync(m => m.Content = $"У пользователя {Context.User.Username} нет прав доступа для создания аудио панели");
            return;
        }

        await AudioPanelUtils.CreateAudioPanel(this, _dbContext, guild.Id, channel.Id);
    }

    [SubSlashCommand("сохранить_трек", "Сохраняет аудио трек из предыдущего сообщения")]
    public async Task UploadTrackFromMessage()
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (Context.Guild is not { } guild || Context.Channel is not { } channel)
        {
            await ModifyResponseAsync(m => m.Content = "Канал не найден");
            return;
        }

        if (await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext) is Error)
        {
            await ModifyResponseAsync(m => m.Content = $"У пользователя { Context.User.Username } нет прав доступа для сохранения аудио");
            return;
        }

        RestMessage? message = null;
        await foreach (var msg in Context.Client.Rest.GetMessagesAsync(channel.Id))
        {
            if (msg.Author.IsBot)
                continue;

            if (msg.Attachments != null && msg.Attachments.Count > 0 && !string.IsNullOrEmpty(msg.Content))
            {
                message = msg;
                break;
            }
        }

        if (message == null)
        {
            await ModifyResponseAsync(m => m.Content = "Файл не найден");
            return;
        }

        if (message.Attachments.Count == 0 || message.Attachments[0] == null || string.IsNullOrEmpty(message.Content))
        {
            await ModifyResponseAsync(m => m.Content = "В сообщении нет вложений или названия");
            return;
        }

        if (!message.Attachments[0].FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
            && message.Attachments[0].ContentType != "audio/mpeg")
        {
            await ModifyResponseAsync(m => m.Content = "Не верный формат файла");
            return;
        }

        var httpClient = _httpClientFactory.CreateClient();
        using var downloadStream = await httpClient.GetStreamAsync(message.Attachments[0].Url);
        var result = await AudioFilesService.TrySaveFile(_dbContext, downloadStream, message.Attachments[0].Size, message.Content, guild.Id);

        if (result is not Success<AudioTrack>)
        {
            await ModifyResponseAsync(m => m.Content = result.Message);
            return;
        }

        await AudioPanelUtils.CreateAudioPanel(this, _dbContext, guild.Id, channel.Id);
    }

    [SubSlashCommand("удалить_трек", "Удаляет трек по названию")]
    public async Task DeleteAudioTrack(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            InteractionMessageProperties imsgp = new()
            {
                Content = "Неверное название.",
                Flags = MessageFlags.Ephemeral
            };

            await RespondAsync(InteractionCallback.Message(imsgp));
            return;
        }

        var r = await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (Context.Guild is not { } guild || Context.Channel is not { } channel)
        {
            await ModifyResponseAsync(m => m.Content = "Канал не найден");
            return;
        }

        if (await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext) is Error)
        {
            await ModifyResponseAsync(m => m.Content = $"У пользователя {Context.User.Username} нет прав доступа для удаления аудио");
            return;
        }

        var result = AudioFilesService.DeleteFile(_dbContext, guild.Id, title);

        if (result is not Success)
        {
            await ModifyResponseAsync(m => m.Content = result.Message);
            return;
        }

        await AudioPanelUtils.CreateAudioPanel(this, _dbContext, guild.Id, channel.Id);
    }



    private async Task TryDisconnectTheBot()
    {
        if (Context.Guild is null) return;

        var guildId = Context.Guild.Id;

        if (!_viContainer.VoiceInstances.TryGetValue(guildId, out var voiceInstance) || voiceInstance is null)
            return;

        if (!_viContainer.VoiceInstances.TryRemove(new(guildId, voiceInstance)))
            return;

        try
        {
            await voiceInstance.Client.CloseAsync();
        }
        finally
        {
            try
            {
                voiceInstance.Dispose();
            }
            finally
            {
                await Context.Client.UpdateVoiceStateAsync(new(guildId, null));
            }
        }
    }
}
