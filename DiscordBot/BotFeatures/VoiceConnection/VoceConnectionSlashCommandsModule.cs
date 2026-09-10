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

[SlashCommand("аудио", "Команды, связанные с аудио каналами")]
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
            await ModifyResponseAsync(m => m.Content = "Канал не найден.");
            return;
        }

        var user = Context.User;
        if (await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext) is Error)
        {
            await ModifyResponseAsync(m => m.Content = "У тебя нет прав доступа.");
            return;
        }

        ulong channelId;
        if (guild.VoiceStates.TryGetValue(user.Id, out var voiceState))
            channelId = voiceState.ChannelId.GetValueOrDefault();
        else
        {
            await ModifyResponseAsync(m => m.Content = "Ты должен находиться в голосовм канале.");
            return;
        }

        var guildId = guild.Id;

        if (!_viContainer.VoiceInstances.TryAdd(guildId, null))
        {
            await ModifyResponseAsync(m => m.Content = "Бот уже находится в голосовом канале.");
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

            await ModifyResponseAsync(m => m.Content = "Не удалось зарегистрировать соединение.");
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

        await ModifyResponseAsync(m => m.Content = "Готово.");
    }

    [SubSlashCommand("покинуть", "Покинуть голосовой канал")]
    public async Task LeaveChannel()
    {
        if (Context.Guild is not { } guild)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("Канал не найден.")
                    .WithFlags(MessageFlags.Ephemeral)));
            return;
        }

        if (await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext) is Error)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("У тебя нет прав доступа.")
                    .WithFlags(MessageFlags.Ephemeral)));
            return;
        }

        var guildId = guild.Id;

        if (!_viContainer.VoiceInstances.TryGetValue(guildId, out var voiceInstance) || voiceInstance is null)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("Бот не подключен к голосовому каналу.")
                    .WithFlags(MessageFlags.Ephemeral)));
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

        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties()
                    .WithContent("Бот покинул голосовой канал.")
                    .WithFlags(MessageFlags.Ephemeral)));
    }

    [SubSlashCommand("создать_аудио_панель", "Создает аудио панель в этом канале")]
    public async Task CreateAudioPanel()
    {
        await RespondAsync(InteractionCallback.DeferredMessage());

        if (Context.Guild is not { } guild)
        {
            await ModifyResponseAsync(m => m.Content = "Канал не найден.");
            return;
        }

        if (await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext) is Error)
        {
            await ModifyResponseAsync(m => m.Content = "У тебя нет прав доступа.");
            return;
        }

        await CreateAudioPanel(guild.Id);
    }

    [SubSlashCommand("сохранить_трек", "Сохраняет аудио трек из предыдущего сообщения.")]
    public async Task UploadTrackFromMessage()
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (Context.Guild is not { } guild || Context.Channel is not { } channel)
        {
            await ModifyResponseAsync(m => m.Content = "Канал не найден.");
            return;
        }

        if (await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext) is Error)
        {
            await ModifyResponseAsync(m => m.Content = "У тебя нет прав доступа.");
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
            await ModifyResponseAsync(m => m.Content = "В сообщении нет вложений или названия.");
            return;
        }

        if (message.Attachments[0].Size > 9 * 1024 * 1024)
        {
            await ModifyResponseAsync(m => m.Content = "Файл слишком большой.");
            return;
        }

        if (!message.Attachments[0].FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
            && message.Attachments[0].ContentType != "audio/mpeg")
        {
            await ModifyResponseAsync(m => m.Content = "Не верный формат файла.");
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

        await RebuildAudioPanel(guild.Id);

        await ModifyResponseAsync(m => m.Content = "Готово.");
    }

    [SubSlashCommand("удалить_трек", "Удаляет трек по названию.")]
    public async Task DeleteAudioTrack(string title)
    {
        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (string.IsNullOrWhiteSpace(title))
        {
            await ModifyResponseAsync(m => m.Content = "Неверное название.");
            return;
        }

        if (Context.Guild is not { } guild || Context.Channel is not { } channel)
        {
            await ModifyResponseAsync(m => m.Content = "Канал не найден.");
            return;
        }

        if (await PrivilegedUsersService.IsAuthorizedRoleOrOwner(Context, _dbContext) is Error)
        {
            await ModifyResponseAsync(m => m.Content = "У тебя нет прав доступа.");
            return;
        }

        var result = AudioFilesService.DeleteFile(_dbContext, title);

        if (result is not Success)
        {
            await ModifyResponseAsync(m => m.Content = result.Message);
            return;
        }

        await RebuildAudioPanel(guild.Id);

        await ModifyResponseAsync(m => m.Content = "Готово.");
    }


    private async Task CreateAudioPanel(ulong guildId)
    {
        var audioTracks = _dbContext.AudioTracks
            .AsNoTracking()
            .Include(t => t.Guild)
            .Where(t => t.Guild.Id == guildId)
            .ToList();

        if (audioTracks == null || audioTracks.Count == 0)
        {
            await ModifyResponseAsync(m => m.Content = "Отсутствуют данные в базе данных");
            return;
        }

        AudioPanel? audioPanel = _dbContext.AudioPanels
            .FirstOrDefault(ap => ap.GuildId == guildId);

        if (audioPanel == null)
        {
            audioPanel = new();
            _dbContext.AudioPanels.Add(audioPanel);
        }
        else
        {
            //remove prev panel
            await TryDeleteAudioPanelMessages(audioPanel, Context);
        }

        int audioTracksIndex = 0;
        int messagesCount = audioTracks.Count / 25;
        var remainder = audioTracks.Count % 25;
        if (remainder != 0)
        {
            messagesCount++;
        }

        bool isAnswered = false;
        InteractionMessageProperties[] panelMessages = new InteractionMessageProperties[messagesCount];

        for (int j = 0; j < messagesCount; j++)
        {
            InteractionMessageProperties mProps = new();
            panelMessages[j] = mProps;

            int currComponentsCount = 0;
            ActionRowProperties currentActionRow = new();
            mProps.AddComponents(currentActionRow);

            for (int i = 0; i < audioTracks.Count && i < 25; i++)
            {
                //Add stop button
                //if (audioTracksIndex == 0)
                //{
                //    ButtonProperties stopButton = new($"{VoiceConnectionConstants.VoicePanelStopButtonId}", "ОСТАНОВИТЬ", NetCord.ButtonStyle.Danger);
                //    currentActionRow.AddComponents(stopButton);
                //    currComponentsCount++;
                //    continue;
                //}

                var title = audioTracks[audioTracksIndex].Title;
                ButtonProperties button = new($"{VoiceConnectionConstants.VoicePanelButtonId}:{title}", title, NetCord.ButtonStyle.Primary);
                currentActionRow.AddComponents(button);
                currComponentsCount++;

                //Add new action row if its not last itteration
                if (currComponentsCount == 5
                    && i != audioTracks.Count - 1
                    && i != 24)
                {
                    currComponentsCount = 0;
                    currentActionRow = new();
                    mProps.AddComponents(currentActionRow);
                }

                audioTracksIndex++;
            }

            isAnswered = true;
        }

        ulong[] msgIds = new ulong[panelMessages.Length];
        for (int k = 0; k < panelMessages.Length; k++)
        {
            var msg = await FollowupAsync(panelMessages[k]);
            msgIds[k] = msg.Id;
        }

        if (Context.Guild != null)
            audioPanel.GuildId = Context.Guild.Id;

        audioPanel.ChannelId = Context.Channel.Id;
        audioPanel.MessageIds = msgIds;

        try
        {
            _dbContext.SaveChanges();
        }
        catch
        {
            await ModifyResponseAsync(m => m.Content = "Ошибка сохранения панели в базу данных.");
            return;
        }

        if (isAnswered == false)
        {
            await ModifyResponseAsync(m => m.Content = "Ошибка создания панели.");
        }
    }

    private async Task RebuildAudioPanel(ulong guildId)
    {
        var audioTracks = _dbContext.AudioTracks
            .AsNoTracking()
            .Include(t => t.Guild)
            .Where(t => t.Guild.Id == guildId)
            .ToList();

        if (audioTracks == null || audioTracks.Count == 0)
            return;

        AudioPanel? audioPanel = _dbContext.AudioPanels
            .FirstOrDefault(ap => ap.GuildId == guildId);

        if (audioPanel == null || audioPanel.ChannelId <= 0 || audioPanel.GuildId <= 0 
            || audioPanel.MessageIds == null || audioPanel.MessageIds.Length == 0)
        {
            return;
        }
        else
        {
            //Remove prev panel
            await TryDeleteAudioPanelMessages(audioPanel, Context);
        }

        int audioTracksIndex = 0;
        int messagesCount = audioTracks.Count / 25;
        var remainder = audioTracks.Count % 25;
        if (remainder != 0)
        {
            messagesCount++;
        }

        MessageProperties[] panelMessages = new MessageProperties[messagesCount];

        for (int j = 0; j < messagesCount; j++)
        {
            MessageProperties mProps = new();
            panelMessages[j] = mProps;

            int currComponentsCount = 0;
            ActionRowProperties currentActionRow = new();
            mProps.AddComponents(currentActionRow);

            for (int i = 0; i < audioTracks.Count && i < 25; i++)
            {
                //if (audioTracksIndex == 0)
                //{
                //    ButtonProperties stopButton = new($"{VoiceConnectionConstants.VoicePanelStopButtonId}", "ОСТАНОВИТЬ", NetCord.ButtonStyle.Danger);
                //    currentActionRow.AddComponents(stopButton);
                //    currComponentsCount++;
                //    continue;
                //}

                var title = audioTracks[audioTracksIndex].Title;
                ButtonProperties button = new($"{VoiceConnectionConstants.VoicePanelButtonId}:{title}", title, NetCord.ButtonStyle.Primary);
                currentActionRow.AddComponents(button);
                currComponentsCount++;

                //Add new action row if its not last itteration
                if (currComponentsCount == 5
                    && i != audioTracks.Count - 1
                    && i != 24)
                {
                    currComponentsCount = 0;
                    currentActionRow = new();
                    mProps.AddComponents(currentActionRow);
                }

                audioTracksIndex++;
            }
        }

        ulong[] msgIds = new ulong[panelMessages.Length];
        for (int k = 0; k < panelMessages.Length; k++)
        {
            var msg = await Context.Client.Rest.SendMessageAsync(audioPanel.ChannelId, panelMessages[k]);
            if (msg != null)
                msgIds[k] = msg.Id;
        }

        if (Context.Guild != null)
            audioPanel.GuildId = Context.Guild.Id;

        audioPanel.ChannelId = Context.Channel.Id;
        audioPanel.MessageIds = msgIds;

        try
        {
            _dbContext.SaveChanges();
        }
        catch { }
    }

    private static async Task TryDeleteAudioPanelMessages(AudioPanel audioPanel, ApplicationCommandContext context)
    {
        try
        {
            if (audioPanel.MessageIds != null && audioPanel.MessageIds.Length > 0)
            {
                foreach (var msgId in audioPanel.MessageIds)
                {
                    await context.Client.Rest.DeleteMessageAsync(audioPanel.ChannelId, msgId);
                }
            }
        }
        catch { }
    }

    private async Task TryDisconnectTheBot()
    {
        if (Context.Guild == null)
            return;

        var guildId = Context.Guild.Id;

        if (!_viContainer.VoiceInstances.TryGetValue(guildId, out var voiceInstance) || voiceInstance is null)
            return;

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
    }
}
