using DiscordBotApi.Data.Channels;
using DiscordBotApi.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize(Roles = "Admin, Moderator")]
public class DiscordChannelsController(ApplicationDbContext context, GatewayClient client) : ControllerBase
{
    readonly ApplicationDbContext _context = context;
    readonly GatewayClient _client = client;

    [HttpGet("/channels/get-channels")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetChannels()
    {
        var channels = _context.Channels
            .AsNoTracking()
            .Select(c => new DiscordChannelGetDto()
            {
                Id = c.Id.ToString(),
                Name = c.Name,
                IsTextChannel = c.IsTextChannel,
                GuildId = c.Guild.Id.ToString(),
            })
            .ToList();

        if (channels == null || channels.Count == 0)
        {
            return NotFound();
        }

        return Ok(channels);
    }

    [HttpGet("/channels/get-discord-channels")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetChannelsFromDiscord([FromQuery] ulong guildId, CancellationToken cancellationToken)
    {
        var channels = await _client.Rest.GetGuildChannelsAsync(guildId, cancellationToken: cancellationToken);

        if (channels == null || channels.Count == 0)
        {
            return NotFound();
        }

        var channelDtos = channels.Select(c => new DiscordChannelGetDto()
        {
            Id = c.Id.ToString(),
            Name = c.Name,
            IsTextChannel = c is not VoiceGuildChannel,
            GuildId = guildId.ToString(),
        });

        return Ok(channelDtos);
    }
}
