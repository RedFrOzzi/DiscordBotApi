using DiscordBotApi.Data.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetCord.Gateway;
using NetCord.Rest;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("bot-messages")]
[Authorize(Roles = "Admin, Moderator")]
public class BotMessagesController(GatewayClient client) : ControllerBase
{
    readonly GatewayClient _client = client;

    //------------------------------------------------------SEND-MESSAGES-------------------------------------------------------------------------------------------------------------

    [HttpPost("send-props")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SendMessage([FromQuery] ulong channelId, [FromBody] SendMessageDto message, CancellationToken cancellationToken)
    {
        if (channelId == default || message == null)
            return BadRequest("Parameters error");

        if (_client == null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        var mProps = message.ConvertoToMessageProperties();

        await _client.Rest.SendMessageAsync(channelId, mProps, cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpPost("send-message")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SendMessage([FromQuery] ulong channelId, [FromBody] string message, CancellationToken cancellationToken)
    {
        if (channelId == default || string.IsNullOrEmpty(message))
            return BadRequest("Parameters error");

        if (_client == null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        await _client.Rest.SendMessageAsync(channelId, message, cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpPost("send-embed")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SendMessageWithEmbed([FromQuery] ulong channelId, [FromBody] SendEmbedDto embed, CancellationToken cancellationToken)
    {
        if (channelId == default || embed == null)
            return BadRequest("Parameters error");

        if (_client == null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        var dto = embed.ConvertToEmbedProperties();
        MessageProperties mProps = new();
        mProps.AddEmbeds(dto);

        await _client.Rest.SendMessageAsync(channelId, mProps, cancellationToken: cancellationToken);

        return Ok();
    }

    //------------------------------------------------------DELETE-MESSAGES-------------------------------------------------------------------------------------------------------------

    [HttpDelete("delete-bot-messages")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteBotMessages([FromQuery] ulong channelId, CancellationToken cancellationToken)
    {
        if (channelId == default)
            return BadRequest("Parameters error");

        if (_client == null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        var botMessages = await GetBotMessages(_client.Rest, channelId);
        await _client.Rest.DeleteMessagesAsync(channelId, botMessages, cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpDelete("delete-message")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteMessage([FromQuery] ulong channelId, [FromQuery] ulong massageId, CancellationToken cancellationToken)
    {
        if (channelId == default || massageId == default)
            return BadRequest("Parameters error");

        if (_client == null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        await _client.Rest.DeleteMessageAsync(channelId, massageId, cancellationToken: cancellationToken);

        return Ok();
    }

    [HttpDelete("delete-messages")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteMessages([FromQuery] ulong channelId, [FromQuery] ulong userId, CancellationToken cancellationToken)
    {
        if (channelId == default || userId == default)
            return BadRequest("Parameters error");

        if (_client == null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        var messageIds = await GetUserMessages(_client.Rest, channelId, userId);
        await _client.Rest.DeleteMessagesAsync(channelId, messageIds, cancellationToken: cancellationToken);

        return Ok();
    }


    static async Task<List<ulong>> GetUserMessages(RestClient client, ulong channelId, ulong userId)
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

    static async Task<List<ulong>> GetBotMessages(RestClient client, ulong channelId)
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
