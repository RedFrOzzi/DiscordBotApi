using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway.Voice;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Serilog;
using System.Diagnostics;

namespace DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;

public class VoiceConnectionButtonsInteractionModule(ApplicationDbContext dbContext,
    VoiceInstancesContainer voiceInstancesContainer,
    AsyncTimersCollection timers)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    readonly ApplicationDbContext _dbContext = dbContext;
    readonly VoiceInstancesContainer _voiceInstancesContainer = voiceInstancesContainer;
    readonly AsyncTimersCollection _timers = timers;

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

        var track = _dbContext.AudioTracks
           .AsNoTracking()
           .Where(t => t.Guild.Id == guild.Id)
           .FirstOrDefault(t => t.Title == title);

        if (track == null)
        {
            Log.Error("User with id: {0} invoke audio play, but track with title: {1} was not found.", Context?.User?.Id, title);
            await FollowupAsync(new() { Content = "Трек не найден" });
            return;
        }

        if (!File.Exists(track.Path))
        {
            Log.Error("User with id: {0} invoke audio play, but path was not correct", Context?.User?.Id);
            await FollowupAsync(new() { Content = "Некорректный путь файла" });
            return;
        }

        //Connect to channel, if not already connected
        if (!_voiceInstancesContainer.VoiceInstances.ContainsKey(guild.Id))
        {
            ulong channelId;

            if (Context.User == null)
            {
                await FollowupAsync(new() { Content = "Пользователь не найден" });
                return;
            }

            if (guild.VoiceStates.TryGetValue(Context.User.Id, out var voiceState))
                channelId = voiceState.ChannelId.GetValueOrDefault();
            else
            {
                await FollowupAsync(new() { Content = "Пользователь не в голосовом канале." });
                return;
            }

            var guildId = guild.Id;

            _voiceInstancesContainer.VoiceInstances.TryAdd(guildId, null);

            VoiceClient? vc;
            try
            {
                vc = await Context.Client.JoinVoiceChannelAsync(guildId, channelId, new());
            }
            catch
            {
                _voiceInstancesContainer.VoiceInstances.TryRemove(item: new(guildId, null));

                await Context.Client.UpdateVoiceStateAsync(new(guildId, null));

                throw;
            }

            VoiceInstance vi = new(vc);

            if (!_voiceInstancesContainer.VoiceInstances.TryUpdate(guildId, vi, null))
            {
                vi.Dispose();

                await Context.Client.UpdateVoiceStateAsync(new(guildId, null));
                await FollowupAsync(new() { Content = "Не удалось зарегистрировать соединение." });
                return;
            }

            vc.Disconnect += args =>
            {
                if (args.Reconnect)
                    return default;

                if (_voiceInstancesContainer.VoiceInstances.TryRemove(item: new(guildId, vi)))
                    vi.Dispose();

                return default;
            };

            try
            {
                await vc.StartAsync();
            }
            catch
            {
                if (_voiceInstancesContainer.VoiceInstances.TryRemove(item: new(guildId, vi)))
                {
                    vi.Dispose();

                    await Context.Client.UpdateVoiceStateAsync(new(guildId, null));
                }

                throw;
            }
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

        var ffmpegPath = Environment.GetEnvironmentVariable("FFMPEG_FILE_PATH") ?? "ffmpeg";

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

        _timers.CreateOrResetTimer(guild.Id, TryDisconnectTheBot);
    }

    [ComponentInteraction(VoiceConnectionConstants.VoicePanelStopButtonId)]
    public async Task HandleStopAudioButton()
    {
        await RespondAsync(InteractionCallback.DeferredModifyMessage);

        if (Context.Guild is not { } guild)
        {
            return;
        }

        if (!_voiceInstancesContainer.VoiceInstances.TryGetValue(guild.Id, out var voiceInstance) || voiceInstance is null)
        {
            return;
        }

        voiceInstance.StopPlaying();
    }


    private async Task TryDisconnectTheBot()
    {
        if (Context.Guild == null)
            return;

        var guildId = Context.Guild.Id;

        if (!_voiceInstancesContainer.VoiceInstances.TryGetValue(guildId, out var voiceInstance) || voiceInstance is null)
            return;

        if (_voiceInstancesContainer.VoiceInstances.TryRemove(item: new(guildId, voiceInstance)))
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
