using DiscordBotApi.Data.Users;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot;
using Microsoft.AspNetCore.Mvc;
using NetCord;
using NetCord.Rest;

namespace DiscordBotApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DiscordGuildsController :ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly DiscordBotBackgroundService _botService;

        public DiscordGuildsController(ApplicationDbContext context, DiscordBotBackgroundService botService)
        {
            _context = context;
            _botService = botService;
        }

        [HttpPut("/guild/save-data")]
        [ProducesResponseType(201)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> SaveDataToDatabase([FromQuery] ulong guildId, CancellationToken cancellationToken)
        {
            if (_botService?.Client == null)
            {
                return BadRequest("Bot service is not working");
            }

            List<DiscordUser> allUsers = await _context.GetUsers(cancellationToken);
            List<GuildUser> guildUsers = [];
            var guild = await _botService.Client.GetGuildAsync(guildId, cancellationToken: cancellationToken);
            await foreach (var u in _botService.Client.GetGuildUsersAsync(guildId))
            {
                guildUsers.Add(u);
            }

            if (_context.SaveGuildData(guild, allUsers, guildUsers))
            {
                return Created();
            }

            return Problem(statusCode: 500, title: "Not saved", detail: "Database error");
        }

        [HttpPut("/guild/update-data")]
        [ProducesResponseType(201)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> UpdateDataToDatabase([FromQuery] ulong guildId, CancellationToken cancellationToken)
        {
            if (_botService?.Client == null)
            {
                return BadRequest("Bot service is not working");
            }

            var guildFromDb = await _context.GetGuild(guildId, cancellationToken: cancellationToken);
            if (guildFromDb == null)
            {
                return NotFound();
            }
            var channels = await _botService.Client.GetGuildChannelsAsync(guildId, cancellationToken: cancellationToken);
            var allDbChannels = await _context.GetChannelsAsync(guildId, cancellationToken: cancellationToken);

            if (await _context.UpdateGuildData(guildFromDb, allDbChannels, channels, cancellationToken))
            {
                return Created();
            }

            return Problem(statusCode: 500, title: "Not saved", detail: "Database error");
        }
    }
}
