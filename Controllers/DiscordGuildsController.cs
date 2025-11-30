using DiscordBotApi.Data.Users;
using DiscordBotApi.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetCord;
using NetCord.Gateway;

namespace DiscordBotApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(Roles = "Admin")]
    public class DiscordGuildsController :ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly GatewayClient _client;

        public DiscordGuildsController(ApplicationDbContext context, [FromKeyedServices("client")] GatewayClient client)
        {
            _context = context;
            _client = client;
        }

        [HttpPut("/guild/save-data")]
        [ProducesResponseType(201)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> SaveDataToDatabase([FromQuery] ulong guildId, CancellationToken cancellationToken)
        {
            if (_client == null)
            {
                return BadRequest("Bot service is not working");
            }

            if (_context.IsGuildExistInDb(guildId, out _))
            {
                return BadRequest("Guild already exist in the database");
            }

            List<DiscordUser> allUsers = await _context.GetUsers(cancellationToken);
            List<GuildUser> guildUsers = [];
            var guild = await _client.Rest.GetGuildAsync(guildId, cancellationToken: cancellationToken);
            await foreach (var u in _client.Rest.GetGuildUsersAsync(guildId))
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
            if (_client == null)
            {
                return BadRequest("Bot service is not working");
            }

            if (!_context.IsGuildExistInDb(guildId, out var guildFromDb))
            {
                return NotFound();
            }

            var channels = await _client.Rest.GetGuildChannelsAsync(guildId, cancellationToken: cancellationToken);
            var allDbChannels = await _context.GetChannelsAsync(guildId, cancellationToken: cancellationToken);

            if (await _context.UpdateGuildData(guildFromDb!, allDbChannels, channels, cancellationToken))
            {
                return Created();
            }

            return Problem(statusCode: 500, title: "Not saved", detail: "Database error");
        }
    }
}
