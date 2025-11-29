using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;

namespace DiscordBotApi.DiscordBot.Services
{
    public class UpdateUsersService
    {
        private readonly IServiceScopeFactory _serviceFactory;

        bool _isInUpdateState;
        bool _isCancelled;

        public UpdateUsersService(IServiceScopeFactory serviceFactory)
        {
            _serviceFactory = serviceFactory;
            _isCancelled = false;
            _isInUpdateState = false;
        }

        public bool IsInUpdateState => _isInUpdateState;

        /// <summary>
        /// Updates user properties
        /// </summary>
        /// <param name="guildId">Guild in which users are updated</param>
        public void BeginUpdate(ulong guildId)
        {
            Console.WriteLine("Started background update job");

            Task.Factory.StartNew(() =>
            {
                using var scope = _serviceFactory.CreateScope();
                var client = scope.ServiceProvider.GetKeyedService<GatewayClient>("client");
                if (client == null)
                {
                    Console.WriteLine("Bot service is not working");
                    return;
                }

                if (_isInUpdateState)
                {
                    Console.WriteLine("Cant proceed, data is currently updating");
                    return;
                }

                _isInUpdateState = true;
                _isCancelled = false;

                var guild = client.Rest.GetGuildAsync(guildId).GetAwaiter().GetResult();
                if (guild == null)
                {
                    _isInUpdateState = false;
                    return;
                }
                var guildUsers = client.Rest.GetGuildUsersAsync(guild.Id).ToListAsync().GetAwaiter().GetResult();
                if (guildUsers == null || guildUsers.Count == 0)
                {
                    _isInUpdateState = false;
                    return;
                }
                int usersCount = guildUsers.Count;

                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (context == null)
                {
                    _isInUpdateState = false;
                    return;
                }

                foreach (var user in guildUsers)
                {
                    if (_isCancelled)
                    {
                        Console.WriteLine($"Background update job canceled");
                        _isInUpdateState = false;
                        return;
                    }
                    var imgUrl = user.GetAvatarUrl();
                    var rolesFromDiscord = user.GetRoles(guild).ToArray();
                    var dbUser = context.Users.FirstOrDefault(u => u.Id == user.Id);

                    if (dbUser == null)
                    {
                        usersCount--;
                        Console.WriteLine($"Users to update left: {usersCount}");
                        continue;
                    }

                    //Set image url
                    dbUser.ImageURL = imgUrl?.ToString();

                    usersCount--;
                    Console.WriteLine($"Users to update left: {usersCount}");
                    Task.Delay(3000).GetAwaiter().GetResult();
                }

                _isInUpdateState = false;
                Console.WriteLine($"Background update job finished");
            },
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void CancelUpdate()
        {
            _isCancelled = true;
            _isInUpdateState = false;
        }
    }
}
