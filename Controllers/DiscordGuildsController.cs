using DiscordBotApi.Data.Guilds;
using DiscordBotApi.Database;
using DiscordBotApi.Services.GuildUpdater;
using DiscordBotApi.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Services;
using System.Collections;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("guilds")]
[Authorize(Roles = "Admin, Moderator")]
public class DiscordGuildsController(ApplicationDbContext context, GatewayClient client, GuildUpdateQueue queue) : ControllerBase
{
    readonly ApplicationDbContext _context = context;
    readonly GatewayClient _client = client;
    readonly GuildUpdateQueue _queue = queue;

    [HttpGet("all-guilds")]
    [ProducesResponseType<List<DiscordGuildGetDto>>(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetGuilds()
    {
        var guilds = _context.Guilds
            .AsNoTracking()
            .Select(g => new DiscordGuildGetDto()
            {
                Id = g.Id.ToString(),
                Name = g.Name,
                OwnerId = g.Owner.Id.ToString(),
                UserIds = g.Users.Select(u => u.Id.ToString()).ToArray(),
                ChannelIds = g.Channels.Select(c => c.Id.ToString()).ToArray(),
                IconUrl = g.IconUrl,
            })
            .AsSplitQuery()
            .ToList();

        if (guilds == null || guilds.Count == 0)
            return NotFound();

        return Ok(guilds);
    }

    [HttpGet("guild")]
    [ProducesResponseType<DiscordGuildGetDto>(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetGuild([FromQuery] ulong guildId)
    {
        var guild = _context.Guilds
            .AsNoTracking()
            .Where(g => g.Id == guildId)
            .Select(g => new DiscordGuildGetDto()
                {
                    Id = g.Id.ToString(),
                    Name = g.Name,
                    OwnerId = g.Owner.Id.ToString(),
                    UserIds = g.Users.Select(u => u.Id.ToString()).ToArray(),
                    ChannelIds = g.Channels.Select(c => c.Id.ToString()).ToArray(),
                    IconUrl= g.IconUrl,
                })
            .AsSplitQuery()
            .FirstOrDefault();

        if (guild == null)
            return NotFound();

        return Ok(guild);
    }

    [HttpPost("update-guild-data")]
    [ProducesResponseType<DiscordGuildUpdateAcceptedDto>(202)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UpdateGuildData([FromQuery] ulong guildId)
    {
        if (guildId <= 1)
            return BadRequest("Wrong guild id");

        if (!_queue.TryEnqueue(guildId, out var status))
            return Conflict(new
            {
                Message = "Guild update already in progress",
                status.JobId,
                status.EnqueuedAt
            });

        return Accepted(new DiscordGuildUpdateAcceptedDto()
        {
            JobId = status.JobId.ToString(),
            GuildId = guildId.ToString(),
            EnqueuedAt = status.EnqueuedAt
        });
    }
}
