using DiscordBotApi.Data.Users;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities;
using Microsoft.AspNetCore.Mvc;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace DiscordBotApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DiscordGuildUsersController : ControllerBase
    {
        private readonly DiscordBotBackgroundService _botService;
        private readonly ApplicationDbContext _context;
        private readonly UpdateUsersService _updateService;

        public DiscordGuildUsersController(DiscordBotBackgroundService botService, ApplicationDbContext context, UpdateUsersService updateService)
        {
            _botService = botService;
            _context = context;
            _updateService = updateService;
        }

        [HttpGet("/get-users-from-discord")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetGuildUsersFromDiscord([FromQuery] ulong guildId, CancellationToken cancellationToken)
        {
            if (_botService?.Client == null)
            {
                return BadRequest("Bot service is not working");
            }

            var guild = await _botService.Client.GetGuildAsync(guildId, cancellationToken: cancellationToken);
            var users = await _botService.Client.GetGuildUsersAsync(guildId).ToListWithConversionAsync(u => u.ConvertToDiscordUser(guild), cancellationToken);

            if (users.Count == 0)
            {
                return NotFound();
            }

            return Ok(users);
        }

        [HttpGet("/get-user-from-discord")]
        [ProducesResponseType(200, Type = typeof(DiscordUserGetDto))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetGuildUserFromDiscord([FromQuery] ulong guildId, [FromQuery] string username, CancellationToken cancellationToken)
        {
            if (_botService?.Client == null)
            {
                return BadRequest("Bot service is not working");
            }

            var guild = await _botService.Client.GetGuildAsync(guildId, cancellationToken: cancellationToken);
            var collection = await _botService.Client.FindGuildUserAsync(guildId, username, 1, cancellationToken: cancellationToken);

            if (collection == null || collection.Count == 0)
            {
                return NotFound();
            }

            var dto = collection[0].ConvertToDiscordUser(guild);
            VoiceState? voiceState;

            try
            {
                voiceState = await _botService.Client.GetGuildUserVoiceStateAsync(guildId, collection[0].Id, cancellationToken: cancellationToken);
            }
            catch
            {
                return Ok(dto);
            }

            dto.AddVoiceState(voiceState);
            return Ok(dto);
        }

        [HttpGet("/get-user-by-id-from-discord")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetGuildUserStateFromDiscord([FromQuery] ulong guildId, [FromQuery] ulong userId, CancellationToken cancellationToken)
        {
            if (_botService?.Client == null)
            {
                return BadRequest("Bot service is not working");
            }

            var guild = await _botService.Client.GetGuildAsync(guildId, cancellationToken: cancellationToken);
            var user = await _botService.Client.GetGuildUserAsync(guildId, userId, cancellationToken: cancellationToken);

            if (user == null)
            {
                return NotFound();
            }

            var dto = user.ConvertToDiscordUser(guild);
            VoiceState? voiceState;

            try
            {
                voiceState = await _botService.Client.GetGuildUserVoiceStateAsync(guildId, user.Id, cancellationToken: cancellationToken);
            }
            catch
            {
                return Ok(dto);
            }

            dto.AddVoiceState(voiceState);
            return Ok(dto);
        }

        [HttpGet("/get-users-from-db")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetUsersFromDb(CancellationToken cancellationToken)
        {
            var users = await _context.GetUsers(cancellationToken);
            if (users == null || users.Count == 0)
            {
                return NotFound();
            }

            return Ok(users.ConvertToDto());
        }

        [HttpPut("/user/save-data")]
        [ProducesResponseType(201)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> SaveDataToDatabase([FromQuery] ulong guildId, CancellationToken cancellationToken)
        {
            if (_botService?.Client == null)
            {
                return BadRequest("Bot service is not working");
            }

            List<GuildUser> users = [];
            await foreach (var user in _botService.Client.GetGuildUsersAsync(guildId))
            {
                users.Add(user);
            }

            if (_context.SaveUsersData(users))
            {
                return Created();
            }

            return Problem(statusCode: 500, title: "Not saved", detail: "Database error");
        }

        [HttpPatch("/update-users")]
        [ProducesResponseType(200)]
        [ProducesResponseType(500)]
        public IActionResult UpdateUsersInDatabase([FromQuery] ulong guildId, CancellationToken cancellationToken)
        {
            _updateService.BeginUpdate(guildId);

            return Ok();
        }

        [HttpPatch("/update-users-cancel")]
        [ProducesResponseType(200)]
        [ProducesResponseType(500)]
        public IActionResult CancelUpdateUsersInDatabase()
        {
            _updateService.CancelUpdate();

            return Ok();
        }
    }
}
