using DiscordBotApi.Data.Guilds;
using DiscordBotApi.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("guilds")]
[Authorize(Roles = "Admin, Moderator")]
public class DiscordGuildsController(ApplicationDbContext context, GatewayClient client) : ControllerBase
{
    readonly ApplicationDbContext _context = context;
    readonly GatewayClient _client = client;

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
                })
            .AsSplitQuery()
            .FirstOrDefault();

        if (guild == null)
            return NotFound();

        return Ok(guild);
    }
}
