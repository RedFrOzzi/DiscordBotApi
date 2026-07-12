using DiscordBotApi.Data.DiscordUsers;
using DiscordBotApi.Database;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;

namespace DiscordBotApi.DiscordBot.Services
{
    public class UpdateUsersService
    {
        public bool IsInUpdateState => _isInUpdateState;
        public int ProcessedPercent => _procecssed;

        private readonly IServiceScopeFactory _serviceFactory;
        private readonly SemaphoreSlim _updateLock = new(1, 1);
        private CancellationTokenSource? _cts;

        bool _isInUpdateState;
        int _procecssed;

        public UpdateUsersService(IServiceScopeFactory serviceFactory)
        {
            _serviceFactory = serviceFactory;
            _isInUpdateState = false;
        }

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
                Console.WriteLine("Update already in progress. Ignoring new request.");
                return;
            }

            using var cts = new CancellationTokenSource();
            _cts = cts;
            var cancelationToken = cts.Token;

            try
            {
                _isInUpdateState = true;
                Console.WriteLine($"Started background update job for guild {guildId}.");

                using var scope = _serviceFactory.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<GatewayClient>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var guild = await client.Rest.GetGuildAsync(guildId).ConfigureAwait(false);
                if (guild == null)
                {
                    Console.WriteLine($"Guild {guildId} not found.");
                    return;
                }

                var guildUsers = await client.Rest.GetGuildUsersAsync(guild.Id).ToListAsync(cancelationToken).ConfigureAwait(false);
                if (guildUsers == null || guildUsers.Count == 0)
                {
                   Console.WriteLine($"No users found in guild {guildId}.");
                    return;
                }

                var userIds = guildUsers.Select(u => u.Id).ToList();
                var dbUsersDict = await dbContext.DiscordUsers
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, cancelationToken)
                    .ConfigureAwait(false);

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
                        Console.WriteLine($"User {discordUser.Id} not in DB, skipping.");
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
                       Console.WriteLine($"Users to update left: {total - processed}");
                        _procecssed = processed;
                    }

                    await Task.Delay(3000, cancelationToken).ConfigureAwait(false);
                }

                // Apply all updates in a single batch.
                if (usersToUpdate.Count > 0)
                {
                    dbContext.DiscordUsers.UpdateRange(usersToUpdate);
                    await dbContext.SaveChangesAsync(cancelationToken).ConfigureAwait(false);
                   Console.WriteLine($"Updated {usersToUpdate.Count} users in guild {guildId}.");
                }

               Console.WriteLine($"Background update job finished for guild {guildId}.");
            }
            catch (OperationCanceledException)
            {
               Console.WriteLine("Background update job was canceled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} Error during user update for guild {guildId}.");
            }
            finally
            {
                _isInUpdateState = false;
                _cts = null;
                _updateLock.Release();
            }
        }
    }
}
