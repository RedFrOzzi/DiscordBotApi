using DiscordBotApi.Data.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetCord.Gateway;
using NetCord.Rest;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("/BotMessages")]
[Authorize(Roles = "Admin")]
public class BotMessagesController(GatewayClient client) : ControllerBase
{
    readonly GatewayClient _client = client;

    //------------------------------------------------------SEND-MESSAGES-------------------------------------------------------------------------------------------------------------

    [HttpPost("/send-props")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SendMessage([FromQuery] ulong channelId, [FromBody] MessageProperties messageProps, CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            return BadRequest("Bot service is not working");
        }

        await _client.Rest.SendMessageAsync(channelId, messageProps, cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpPost("/send-message")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SendMessage([FromQuery] ulong channelId, [FromBody] string message, CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            return BadRequest("Bot service is not working");
        }

        await _client.Rest.SendMessageAsync(channelId, message, cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpPost("/send-embed")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SendMessageWithEmbed([FromQuery] ulong channelId, [FromBody] SendEmbedDto embed, CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            return BadRequest("Bot service is not working");
        }

        await _client.Rest.SendMessageAsync(channelId, new MessageProperties().AddEmbeds(embed.Convert()), cancellationToken: cancellationToken);

        return Ok();
    }

    //------------------------------------------------------DELETE-MESSAGES-------------------------------------------------------------------------------------------------------------

    [HttpDelete("/delete-bot-messages")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> DeleteBotMessages([FromQuery] ulong channelId, CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            return BadRequest("Bot service is not working");
        }

        var botMessages = await GetMessages(_client.Rest, channelId);
        await _client.Rest.DeleteMessagesAsync(channelId, botMessages, cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpDelete("/delete-message")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> DeleteMessage([FromQuery] ulong channelId, [FromQuery] ulong massageId, CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            return BadRequest("Bot service is not working");
        }

        await _client.Rest.DeleteMessageAsync(channelId, massageId, cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpDelete("/delete-messages")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> DeleteMessages([FromQuery] ulong channelId, [FromQuery] ulong userId, CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            return BadRequest("Bot service is not working");
        }

        var messageIds = await GetMessages(_client.Rest, channelId, userId);
        await _client.Rest.DeleteMessagesAsync(channelId, messageIds, cancellationToken: cancellationToken);

        return Ok();

        static async Task<List<ulong>> GetMessages(RestClient client, ulong channelId, ulong userId)
        {
            List<ulong> ids = [];
            await foreach (var msg in client.GetMessagesAsync(channelId))
            {
                if (msg == null || msg.Author.Id != userId)
                    continue;

                ids.Add(msg.Id);
            }
            return ids;
        }
    }




    static async Task<List<ulong>> GetMessages(RestClient client, ulong channelId)
    {
        List<ulong> msgIds = [];
        await foreach (var msg in client.GetMessagesAsync(channelId))
        {
            if (msg == null || !msg.Author.IsBot)
                continue;

            msgIds.Add(msg.Id);
        }

        return msgIds;
    }
}
