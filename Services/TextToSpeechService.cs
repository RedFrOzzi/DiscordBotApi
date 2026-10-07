using edge_tts_net;
using NetCord.Gateway.Voice;
using System.Diagnostics;
using System.Threading.Channels;

namespace DiscordBotApi.Services;

public class TextToSpeechService
{
    public async Task StreamTtsToDiscordAsync(string text, TTSOption ttsOptions, VoiceClient voiceClient, string ffmpegPath, CancellationToken ct)
    {
        var edgeTts = new EdgeTTSNet();

        // Bounded channel
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        // TTS pushes chunks into the channel
        var ttsTask = Task.Run(async () =>
        {
            try
            {
                await edgeTts.TTS(text, meta =>
                {
                    if (meta.Type == TTSMetadataType.Audio && meta.Data?.Length > 0)
                    {
                        // Callback is sync, so bridge to async. This blocks only
                        // if the channel is full, which is intentional backpressure.
                        channel.Writer.WriteAsync(meta.Data, ct).AsTask().GetAwaiter().GetResult();
                    }
                },
                option: ttsOptions,
                cancellationToken: ct);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, ct);

        // Start ffmpeg reading from stdin, outputting raw PCM for Discord
        using var ffmpeg = Process.Start(new ProcessStartInfo
        {
            FileName = ffmpegPath,
            ArgumentList =
        {
            "-i", "pipe:0",
            "-f", "f32le",
            "-ar", "48000",
            "-ac", "2",
            "pipe:1"
        },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;

        // Drain channel into ffmpeg stdin, then close stdin
        var ffmpegStdinWriter = Task.Run(async () =>
        {
            try
            {
                await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
                {
                    await ffmpeg.StandardInput.BaseStream.WriteAsync(chunk, ct);
                }
            }
            finally
            {
                ffmpeg.StandardInput.Close();
            }
        }, ct);

        // Stream ffmpeg stdout into Discord (your existing logic)
        await voiceClient.EnterSpeakingStateAsync(new(SpeakingFlags.Microphone));
        using var voiceStream = voiceClient.CreateVoiceStream();
        using var opusEncodeStream = new OpusEncodeStream(
            voiceStream, PcmFormat.Float, VoiceChannels.Stereo, OpusApplication.Audio);

        try
        {
            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(opusEncodeStream, ct);
            await opusEncodeStream.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            ffmpeg.Kill();
            if (ex is not OperationCanceledException
                and not AggregateException { InnerException: OperationCanceledException })
            {
                throw;
            }
        }

        // Ensure background tasks finish
        await Task.WhenAll(ttsTask, ffmpegStdinWriter);
    }
}
