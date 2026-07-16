using DiscordBotApi.Data.Channels;
using DiscordBotApi.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetCord;
using NetCord.Gateway;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize(Roles = "Admin")]
public class DiscordGuildsController(ApplicationDbContext context, GatewayClient client) : ControllerBase
{
    readonly ApplicationDbContext _context = context;
    readonly GatewayClient _client = client;

    //[HttpPut("/guild/save-data")]
    //[ProducesResponseType(201)]
    //[ProducesResponseType(500)]
    //public async Task<IActionResult> SaveDataToDatabase([FromQuery] ulong guildId, CancellationToken cancellationToken)
    //{
    //    if (_client == null)
    //    {
    //        return BadRequest("Bot service is not working");
    //    }

    //    if (_context.IsGuildExistInDb(guildId, out _))
    //    {
    //        return BadRequest("Guild already exist in the database");
    //    }

    //    List<DiscordUser> allUsers = await _context.GetUsers(cancellationToken);
    //    List<GuildUser> guildUsers = [];
    //    var guild = await _client.Rest.GetGuildAsync(guildId, cancellationToken: cancellationToken);
    //    await foreach (var u in _client.Rest.GetGuildUsersAsync(guildId))
    //    {
    //        guildUsers.Add(u);
    //    }

    //    if (_context.SaveOrUpdateGuildAndUsers(guild, allUsers, guildUsers) != null)
    //    {
    //        return Created();
    //    }

    //    return Problem(statusCode: 500, title: "Not saved", detail: "Database error");
    //}
}
