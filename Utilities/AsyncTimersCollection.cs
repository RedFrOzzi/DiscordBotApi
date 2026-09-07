namespace DiscordBotApi.Utilities;

public class AsyncTimersCollection
{
    readonly int _delayInMinutes;
    readonly static Dictionary<ulong, TimerAsync> _timers = []; //guild id as key

    public AsyncTimersCollection(int delayInMinutes, IHostApplicationLifetime lifetime)
    {
        _delayInMinutes = delayInMinutes;

        lifetime.ApplicationStopping.Register(() =>
        {
            StopAllTimers();
        });
    }

    public void CreateOrResetTimer(ulong guildId, Func<Task> onTimeout)
    {
        if (_timers.TryGetValue(guildId, out var timer))
        {
            timer.Reset();
            return;
        }

        TimerAsync newTimer = new(_delayInMinutes, guildId, onTimeout, CleanUpTimer);
        newTimer.Reset();
        _timers.TryAdd(guildId, newTimer);
    }

    public void StopTheTimer(ulong guildId)
    {
        if (_timers.TryGetValue(guildId, out var timer))
        {
            timer?.Stop();
            _timers.Remove(guildId);
        }
    }

    public void StopAllTimers()
    {
        try
        {
            foreach (var timer in _timers.Values)
            {
                timer.Stop();
            }
        }
        catch { }
    }


    private void CleanUpTimer(ulong guildId) => _timers.Remove(guildId);
}
