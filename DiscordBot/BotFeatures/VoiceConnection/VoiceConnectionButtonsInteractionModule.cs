using DiscordBotApi.Database;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Serilog;
using System.Diagnostics;

namespace DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;

public class VoiceConnectionButtonsInteractionModule(ApplicationDbContext dbContext, VoiceInstancesContainer voiceInstancesContainer)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    readonly ApplicationDbContext _dbContext = dbContext;
    readonly VoiceInstancesContainer _voiceInstancesContainer = voiceInstancesContainer;

    [ComponentInteraction(VoiceConnectionConstants.VoicePanelButtonId)]
    public async Task HandleAudioButton(string title)
    {
        if (string.IsNullOrEmpty(title)
            || Context.Guild is not { } guild)
        {
            InteractionMessageProperties imsgp = new()
            {
                Content = "Ошибка сервера.",
                Flags = MessageFlags.Ephemeral
            };

            await RespondAsync(InteractionCallback.Message(imsgp));
            return;
        }

        await RespondAsync(InteractionCallback.DeferredModifyMessage);

        if (!_voiceInstancesContainer.VoiceInstances.ContainsKey(guild.Id))
        {
            await ConnectToVoiceChannel(guild, Context.User);
        }

        var track = _dbContext.AudioTracks
            .AsNoTracking()
            .Where(t => t.Guild.Id == guild.Id)
            .FirstOrDefault(t => t.Title == title);

        if (track == null)
        {
            Log.Error("User with id: {0} invoke audio play, but track with title: {1} was not found.", Context?.User?.Id, title);
            return;
        }

        if (!_voiceInstancesContainer.VoiceInstances.TryGetValue(guild.Id, out var voiceInstance) || voiceInstance is null)
        {
            Log.Error("User with id: {0} invoke audio play, but bot was not connected", Context?.User?.Id);
            return;
        }

        using var job = voiceInstance.TryEnterJob(VoiceJobType.Playing);
        if (job is not { CancellationToken: var cancellationToken })
        {
            return;
        }

        var voiceClient = voiceInstance.Client;
        await voiceClient.EnterSpeakingStateAsync(new(SpeakingFlags.Microphone));

        using var voiceStream = voiceClient.CreateVoiceStream();
        using OpusEncodeStream opusEncodeStream = new(voiceStream,
                                                      PcmFormat.Float,
                                                      VoiceChannels.Stereo,
                                                      OpusApplication.Audio);

        if (!File.Exists(track.Path))
        {
            Log.Error("User with id: {0} invoke audio play, but path was not correct", Context?.User?.Id);
            return;
        }

        var ffmpegPath = Environment.GetEnvironmentVariable("FFMPEG_FILE_PATH");
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            Log.Error("User with id: {0} invoke audio play, but ffmpeg file path was empty", Context?.User?.Id);
            return;
        }

        using var ffmpeg = Process.Start(new ProcessStartInfo
        {
            FileName = ffmpegPath,
            ArgumentList =
            {
                "-i", track.Path,
                "-f", BitConverter.IsLittleEndian ? "f32le" : "f32be",
                "-ar", "48000",
                "-ac", "2",
                "pipe:1",
            },
            RedirectStandardOutput = true,
        })!;

        var ffmpegOutput = ffmpeg.StandardOutput.BaseStream;

        try
        {
            await ffmpegOutput.CopyToAsync(opusEncodeStream, cancellationToken);
            await opusEncodeStream.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ffmpeg.Kill();

            if (ex is not OperationCanceledException and not AggregateException { InnerException: OperationCanceledException })
                throw;
        }
    }

    private async Task ConnectToVoiceChannel(Guild guild, NetCord.User user)
    {
        ulong channelId;
        if (guild.VoiceStates.TryGetValue(user.Id, out var voiceState))
            channelId = voiceState.ChannelId.GetValueOrDefault();
        else
        {
            await ModifyResponseAsync(m => m.Content = "Ты должен находиться в голосовм канале.");
            return;
        }

        var guildId = guild.Id;

        if (!_voiceInstancesContainer.VoiceInstances.TryAdd(guildId, null))
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
            _voiceInstancesContainer.VoiceInstances.TryRemove(item: new(guildId, null));

            await Context.Client.UpdateVoiceStateAsync(new(guildId, null));

            throw;
        }

        VoiceInstance voiceInstance = new(voiceClient);

        if (!_voiceInstancesContainer.VoiceInstances.TryUpdate(guildId, voiceInstance, null))
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

            if (_voiceInstancesContainer.VoiceInstances.TryRemove(item: new(guildId, voiceInstance)))
                voiceInstance.Dispose();

            return default;
        };

        try
        {
            await voiceClient.StartAsync();
        }
        catch
        {
            if (_voiceInstancesContainer.VoiceInstances.TryRemove(item: new(guildId, voiceInstance)))
            {
                voiceInstance.Dispose();

                await Context.Client.UpdateVoiceStateAsync(new(guildId, null));
            }

            throw;
        }
    }
}
