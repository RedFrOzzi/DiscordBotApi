using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DiscordBotApi.Services.GuildUpdater;

public sealed record GuildUpdateStatus(Guid JobId, ulong GuildId, DateTimeOffset EnqueuedAt);
public sealed record GuildUpdateWorkItem(ulong GuildId, GuildUpdateStatus Status);

public sealed class GuildUpdateQueue
{
    private readonly Channel<GuildUpdateWorkItem> _channel =
        Channel.CreateUnbounded<GuildUpdateWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<ulong, GuildUpdateStatus> _active = new();

    public bool TryEnqueue(ulong guildId, out GuildUpdateStatus status)
    {
        var newStatus = new GuildUpdateStatus(Guid.NewGuid(), guildId, DateTimeOffset.UtcNow);

        if (!_active.TryAdd(guildId, newStatus))
        {
            status = _active[guildId];
            return false;
        }

        status = newStatus;
        _channel.Writer.TryWrite(new GuildUpdateWorkItem(guildId, newStatus));
        return true;
    }

    public IAsyncEnumerable<GuildUpdateWorkItem> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);

    internal void Complete(GuildUpdateWorkItem item)
        => _active.TryRemove(item.GuildId, out _);
}