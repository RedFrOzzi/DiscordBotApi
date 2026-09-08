using DiscordBotApi.Data.DiscordUsers;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("guild-users")]
[Authorize(Roles = "Admin, Moderator")]
public class DiscordGuildUsersController(GatewayClient client, ApplicationDbContext context, UpdateUsersService updateService) : ControllerBase
{
    readonly GatewayClient _client = client;
    readonly ApplicationDbContext _context = context;
    readonly UpdateUsersService _updateService = updateService;

    [HttpGet("users-from-discord")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetGuildUsersFromDiscord([FromQuery] ulong guildId, CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            return BadRequest("Bot service is not working");
        }

        var guild = await _client.Rest.GetGuildAsync(guildId, cancellationToken: cancellationToken);
        var users = await _client.Rest.GetGuildUsersAsync(guildId).ToListWithConversionAsync(u => u.ConvertToDiscordUser(guild), cancellationToken);

        if (users.Count == 0)
        {
            return NotFound();
        }

        return Ok(users);
    }

    [HttpGet("user-from-discord")]
    [ProducesResponseType(200, Type = typeof(DiscordUserGetDto))]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetGuildUserFromDiscord([FromQuery] ulong guildId, [FromQuery] string username, CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            return BadRequest("Bot service is not working");
        }

        var guild = await _client.Rest.GetGuildAsync(guildId, cancellationToken: cancellationToken);
        var collection = await _client.Rest.FindGuildUserAsync(guildId, username, 1, cancellationToken: cancellationToken);

        if (collection == null || collection.Count == 0)
        {
            return NotFound();
        }

        var dto = collection[0].ConvertToDiscordUser(guild);
        VoiceState? voiceState;

        try
        {
            voiceState = await _client.Rest.GetGuildUserVoiceStateAsync(guildId, collection[0].Id, cancellationToken: cancellationToken);
        }
        catch
        {
            return Ok(dto);
        }

        dto.AddVoiceState(voiceState);
        return Ok(dto);
    }

    [HttpGet("user-by-id-from-discord")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetGuildUserStateFromDiscord([FromQuery] ulong guildId, [FromQuery] ulong userId, CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            return BadRequest("Bot service is not working");
        }

        var guild = await _client.Rest.GetGuildAsync(guildId, cancellationToken: cancellationToken);
        var user = await _client.Rest.GetGuildUserAsync(guildId, userId, cancellationToken: cancellationToken);

        if (user == null)
        {
            return NotFound();
        }

        var dto = user.ConvertToDiscordUser(guild);
        VoiceState? voiceState;

        try
        {
            voiceState = await _client.Rest.GetGuildUserVoiceStateAsync(guildId, user.Id, cancellationToken: cancellationToken);
        }
        catch
        {
            return Ok(dto);
        }

        dto.AddVoiceState(voiceState);
        return Ok(dto);
    }

    [HttpGet("users-from-db")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetUsersFromDb()
    {
        var users = _context.DiscordUsers
            .AsNoTracking()
            .Select(user => new DiscordUserGetDto()
                {
                    Id = user.Id.ToString(),
                    Username = user.Username,
                    Nickname = user.Nickname,
                    GlobalName = user.GlobalName,
                    ImageURL = user.ImageURL,
                    UserResource = user.UserSpendingResource,
                })
            .ToList();

        if (users == null || users.Count == 0)
        {
            return NotFound();
        }

        return Ok(users);
    }

    [HttpGet("update-progress")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetUsersUpdateProgress(CancellationToken cancellationToken)
    {
        if (_updateService == null)
            return NotFound();

        if (!_updateService.IsInUpdateState)
            return BadRequest("Update is not running");

        return Ok(_updateService.ProcessedPercent);
    }

    [HttpPatch("change-user-resource")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public IActionResult GiveUserIqPoints([FromQuery] ulong userId, [FromQuery] int resourcePointsChange, CancellationToken cancellationToken)
    {
        var user = _context.DiscordUsers.FirstOrDefault(u => u.Id == userId);
        if (user == null)
        {
            return NotFound();
        }

        user.UserSpendingResource += resourcePointsChange;
        if (_context.SaveChanges() == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Database Error",
                Detail = "Error while writing to database"
            });
        }

        return Ok(user.ConverToDto());
    }

    [HttpPatch("update-users")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public IActionResult UpdateUsersInDatabase([FromQuery] ulong guildId, CancellationToken cancellationToken)
    {
        if (guildId <= 0)
            return BadRequest("guild id was not provided");

        _updateService.BeginUpdate(guildId);

        return Ok();
    }

    [HttpPatch("cancel-update-users")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public IActionResult CancelUpdateUsersInDatabase()
    {
        _updateService.CancelUpdate();

        return Ok();
    }
}
