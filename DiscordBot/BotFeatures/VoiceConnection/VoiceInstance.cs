using NetCord.Gateway.Voice;

namespace DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;

public sealed class VoiceInstance(VoiceClient client) : IDisposable
{
    public VoiceClient Client => client;

    static readonly int _jobTypeCount = Enum.GetValues<VoiceJobType>().Length;
    CancellationTokenSource _cancellationTokenSource = new();
    readonly byte[] _jobStatuses = new byte[_jobTypeCount];
    readonly Lock _lock = new();

    public Job? TryEnterJob(VoiceJobType type)
    {
        return Interlocked.CompareExchange(ref _jobStatuses[(int)type], 1, 0) is 0
            ? new(this, type, _cancellationTokenSource.Token)
            : null;
    }

    public void StopPlaying()
    {
        lock (_lock)
        {
            if (Interlocked.CompareExchange(ref _jobStatuses[(int)VoiceJobType.Playing], 0, 1) != 1)
                return;

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            _cancellationTokenSource = new CancellationTokenSource();
        }
    }

    public void Dispose()
    {
        var tokenSource = _cancellationTokenSource;
        tokenSource.Cancel();
        tokenSource.Dispose();
        client.Dispose();
    }

    public readonly record struct Job(VoiceInstance Instance,
                                      VoiceJobType JobType,
                                      CancellationToken CancellationToken) : IDisposable
    {
        public void Dispose()
        {
            Interlocked.Exchange(ref Instance._jobStatuses[(int)JobType], 0);
        }
    }
}

public enum VoiceJobType
{
    Playing = 0,
    Recording = 1,
}
