namespace DiscordBotApi.Utilities;

public class TimerAsync(int delayInMinutes, ulong guildId, Func<Task> onTimeout, Action<ulong> cleanUpCallback)
{
    CancellationTokenSource? _cts;
    readonly TimeSpan _timeout = TimeSpan.FromMinutes(delayInMinutes);
    readonly ulong _guildId = guildId;
    readonly Func<Task> _onTimeout = onTimeout;
    readonly Action<ulong> _cleanUpCallback = cleanUpCallback;
    readonly Lock _lock = new();

    public void Reset()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () =>
            {
                bool completed = false;

                try
                {
                    await Task.Delay(_timeout, token);

                    if (_onTimeout != null)
                        await _onTimeout.Invoke();

                    completed = true;
                }
                catch (TaskCanceledException) { }
                catch (Exception)
                {
                    completed = true;
                }
                finally
                {
                    if (completed)
                        _cleanUpCallback?.Invoke(_guildId);
                }

            }, token);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
