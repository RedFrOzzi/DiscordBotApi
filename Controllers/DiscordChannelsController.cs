using DiscordBotApi.Data.Channels;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot;
using Microsoft.AspNetCore.Mvc;
using NetCord.Rest;

namespace DiscordBotApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DiscordChannelsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly DiscordBotBackgroundService _botService;

        public DiscordChannelsController(ApplicationDbContext context, DiscordBotBackgroundService botService)
        {
            _context = context;
            _botService = botService;
        }

        [HttpGet("/channel/get-channels")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetChannels(CancellationToken cancellationToken)
        {
            var channels = await _context.GetChannelDtosAsync(cancellationToken);
            if (channels == null || channels.Count == 0)
            {
                return NotFound();
            }

            return Ok(channels);
        }

        [HttpPut("/channel/save-data")]
        [ProducesResponseType(201)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> SaveDataToDatabase([FromQuery] ulong guildId, CancellationToken cancellationToken)
        {
            if (_botService?.Client == null)
            {
                return BadRequest("Bot service is not working");
            }

            var guild = await _botService.Client.GetGuildAsync(guildId, cancellationToken: cancellationToken);
            var cahnnels = await guild.GetChannelsAsync(cancellationToken: cancellationToken);
            var dicordGuild = await _context.GetGuild(guildId, cancellationToken);

            if (dicordGuild == null)
            {
                return NotFound();
            }

            if (_context.SaveChannelsData(dicordGuild, cahnnels))
            {
                return Created();
            }

            return Problem(statusCode: 500, title: "Not saved", detail: "Database error");
        }

        [HttpPut("/channel/update-data")]
        [ProducesResponseType(202)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> UpdateDataToDatabase([FromQuery] ulong guildId, CancellationToken cancellationToken)
        {
            if (_botService?.Client == null)
            {
                return BadRequest("Bot service is not working");
            }

            var guild = await _botService.Client.GetGuildAsync(guildId, cancellationToken: cancellationToken);
            var cahnnels = await guild.GetChannelsAsync(cancellationToken: cancellationToken);
            var dicordGuild = await _context.GetGuild(guildId, cancellationToken);

            if (dicordGuild == null)
            {
                return NotFound();
            }

            if (_context.UpdateChannelsData(dicordGuild, cahnnels))
            {
                return Accepted();
            }

            return Problem(statusCode: 500, title: "Not saved", detail: "Database error");
        }
    }
}
