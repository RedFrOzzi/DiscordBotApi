using DiscordBotApi.Data.DiscordUsers;
using DiscordBotApi.Database;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;
using Serilog;

namespace DiscordBotApi.DiscordBot.Services;

public class UpdateUsersService(GatewayClient gateway, IServiceScopeFactory factory)
{
    public bool IsInUpdateState => _isInUpdateState;
    public int ProcessedPercent => _procecssed;

    readonly GatewayClient _gateway = gateway;
    readonly IServiceScopeFactory _factory = factory;
    readonly SemaphoreSlim _updateLock = new(1, 1);
    CancellationTokenSource? _cts;

    bool _isInUpdateState = false;
    int _procecssed;

    /// <summary>
    /// Updates user properties
    /// </summary>
    /// <param name="guildId">Guild in which users are updated</param>
    public void BeginUpdate(ulong guildId)
    {
        Task.Run(() => ExecuteUpdateAsync(guildId));
    }

    public void CancelUpdate()
    {
        _cts?.Cancel();
        _isInUpdateState = false;
    }

    private async Task ExecuteUpdateAsync(ulong guildId)
    {
        if (!await _updateLock.WaitAsync(0))
        {
            Log.Information("Users updater: Update already in progress. Ignoring new request.");
            return;
        }

        using var cts = new CancellationTokenSource();
        _cts = cts;
        var cancelationToken = cts.Token;

        try
        {
            _isInUpdateState = true;
            Log.Information("Started background update job for guild {guildId}.", guildId);

            var guild = await _gateway.Rest.GetGuildAsync(guildId).ConfigureAwait(false);
            if (guild == null)
            {
                Log.Information("Guild {guildId} not found.", guildId);
                return;
            }

            var guildUsers = await _gateway.Rest.GetGuildUsersAsync(guild.Id).ToListAsync(cancelationToken);
            if (guildUsers == null || guildUsers.Count == 0)
            {
                Log.Information("No users found in guild {guildId}.", guildId);
                return;
            }

            var userIds = guildUsers.Select(u => u.Id).ToList();

            using var scope = _factory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var dbUsersDict = await dbContext.DiscordUsers
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancelationToken);

            var usersToUpdate = new List<DiscordUser>();

            int processed = 0;
            int total = guildUsers.Count;

            foreach (var discordUser in guildUsers)
            {
                cancelationToken.ThrowIfCancellationRequested();

                // Skip users not present in the database.
                if (!dbUsersDict.TryGetValue(discordUser.Id, out var dbUser))
                {
                    processed++;
                    Log.Information("User {discordUser.Id} not in DB, skipping.", discordUser.Id);
                    continue;
                }

                // Update properties.
                var imgUrl = discordUser.GetAvatarUrl();
                dbUser.ImageURL = imgUrl?.ToString();

                usersToUpdate.Add(dbUser);
                processed++;

                // Log progress periodically.
                if (processed % 10 == 0 || processed == total)
                {
                    var val = total - processed;
                    Log.Information("Users to update left: {val}", val);
                    _procecssed = processed;
                }

                await Task.Delay(3000, cancelationToken);
            }

            // Apply all updates in a single batch.
            if (usersToUpdate.Count > 0)
            {
                dbContext.DiscordUsers.UpdateRange(usersToUpdate);
                await dbContext.SaveChangesAsync(cancelationToken);
               Log.Information("Updated {usersToUpdate.Count} users in guild {guildId}.", usersToUpdate.Count, guildId);
            }

           Log.Information("Background update job finished for guild {guildId}.", guildId);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Background update job was canceled.");
        }
        catch (Exception ex)
        {
            Log.Information("{ex.Message} Error during user update for guild {guildId}.", ex.Message, guildId);
        }
        finally
        {
            _isInUpdateState = false;
            _cts = null;
            _updateLock.Release();
        }
    }
}
