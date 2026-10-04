using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using Serilog;

namespace DiscordBotApi.Services.GuildUpdater;

public interface IGuildUpdateProcessor
{
    Task ProcessAsync(ulong guildId, CancellationToken ct);
}

public sealed class GuildUpdateProcessor : IGuildUpdateProcessor
{
    private readonly GatewayClient _client;
    private readonly ApplicationDbContext _context;

    public GuildUpdateProcessor(GatewayClient client, ApplicationDbContext context)
    {
        _client = client;
        _context = context;
    }

    public async Task ProcessAsync(ulong guildId, CancellationToken ct)
    {
        var guild = await _client.Rest.GetGuildAsync(guildId, cancellationToken: ct);
        if (guild == null)
        {
            Log.Error("Guild update was triggered, but discord does not found guild with this id: {0}", guildId);
            return;
        }

        var guildUsers = new Dictionary<ulong, GuildUser>();
        await foreach (var u in _client.Rest.GetGuildUsersAsync(guildId))
        {
            ct.ThrowIfCancellationRequested();
            guildUsers[u.Id] = u;
        }

        SaveDiscordDataUtils.SaveOrUpdateUsers(_context, guildUsers);

        var updatedUsers = _context.DiscordUsers
            .Include(u => u.Guilds)
            .AsEnumerable()
            .DistinctBy(u => u.Id)
            .ToDictionary(u => u.Id);

        var owner = updatedUsers.FirstOrDefault(u => u.Key == guild.OwnerId).Value;
        if (owner == null)
        {
            Log.Error("Owner of the updating guild was not found. Guild id: {0}", guildId);
            return;
        }

        var updatedUsersList = updatedUsers
            .Where(u => guildUsers.ContainsKey(u.Key))
            .Select(kvp => kvp.Value)
            .ToList();

        SaveDiscordDataUtils.SaveOrUpdateGuild(_context, guild, updatedUsersList, owner);
        await _context.SaveChangesAsync(ct);

        await Task.Delay(5000, ct);
        var channels = await _client.Rest.GetGuildChannelsAsync(guildId, cancellationToken: ct);
        SaveDiscordDataUtils.SaveOrUpdateChannels(_context, channels, guild);

        await Task.Delay(5000, ct);
        var roles = await _client.Rest.GetGuildRolesAsync(guildId, cancellationToken: ct);
        SaveDiscordDataUtils.SaveOrUpdateRoles(_context, roles, guildUsers, updatedUsersList, guild.Id);

        await _context.SaveChangesAsync(ct);
    }
}