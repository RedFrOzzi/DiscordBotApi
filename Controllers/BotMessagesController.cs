using DiscordBotApi.Data.Messages;
using DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;
using DiscordBotApi.Services;
using edge_tts_net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Rest;
using Serilog;
using System.Security.Claims;

namespace DiscordBotApi.Controllers;

[ApiController]
[Route("bot-messages")]
[Authorize(Roles = "Admin, Moderator")]
public class BotMessagesController(
    GatewayClient client,
    VoiceInstancesContainer voiceInstancesContainer,
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime) : ControllerBase
{
    readonly GatewayClient _client = client;
    readonly VoiceInstancesContainer _voiceInstancesContainer = voiceInstancesContainer;
    readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    readonly IHostApplicationLifetime _lifetime = lifetime;
    readonly static string _ffmpegPath = Environment.GetEnvironmentVariable("FFMPEG_FILE_PATH") ?? "ffmpeg";

    //------------------------------------------------------SEND-MESSAGES-------------------------------------------------------------------------------------------------------------

    [HttpPost("send-reply")]
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

    [HttpPost("send-voice-message")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SendVoiceMessage([FromQuery] ulong channelId, [FromBody] string message, CancellationToken cancellationToken)
    {
        if (channelId <= 1 || string.IsNullOrWhiteSpace(message))
            return BadRequest("Parameters error");

        if (_client == null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        MessageProperties mProps = new()
        {
            Content = message,
            Tts = true,
        };

        await _client.Rest.SendMessageAsync(channelId, mProps, cancellationToken: cancellationToken);

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

    [HttpPost("stream-voice")]
    [ProducesResponseType(202)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> StreamVoice([FromBody] StreamVoiceDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new 
            {
                key = "message",
                message = "Message is required."
            });

        if (dto.GuildId == 0)
            return BadRequest(new
            {
                key = "guild",
                message = "GuildId are required."
            });

        var message = dto.Message;
        var options = dto.Options ?? TTSOption.Default;
        var guildId = dto.GuildId;
        var shutdownToken = _lifetime.ApplicationStopping;

        if (!_voiceInstancesContainer.VoiceInstances.ContainsKey(guildId))
        {
            return BadRequest(new {
                key = "bot",
                message = "Bot is not in voice channel"
            });
        }

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var gatewayClient = sp.GetRequiredService<GatewayClient>();
            var voiceContainer = sp.GetRequiredService<VoiceInstancesContainer>();
            var streamer = sp.GetRequiredService<TextToSpeechService>();

            try
            {
                if (!voiceContainer.VoiceInstances.TryGetValue(guildId, out var voiceInstance) || voiceInstance is null)
                {
                    Log.Error("User with id: {0} invoke audio play, but bot was not connected", User.FindFirstValue(ClaimTypes.NameIdentifier));
                    return;
                }

                // Take a playback job
                using var job = voiceInstance.TryEnterJob(VoiceJobType.Playing);
                if (job is not { CancellationToken: var jobToken })
                    return;

                // Link job cancellation with app shutdown
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    shutdownToken, jobToken);

                var voiceClient = voiceInstance.Client;

                await streamer.StreamTtsToDiscordAsync(
                    message,
                    options,
                    voiceClient,
                    _ffmpegPath,
                    linked.Token);
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                // App is shutting down
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Voice streaming failed for guild {GuildId}", guildId);
            }
        });

        return Accepted();
    }

    [HttpPost("stop-voice-stream")]
    public IActionResult StopVoice([FromBody] string guildId)
    {
        if (string.IsNullOrWhiteSpace(guildId)
            || !ulong.TryParse(guildId, out var id)
            || id <= 1)
            return BadRequest(new 
            {   key = "guild",
                message = "GuildId is required."
            });

        if (!_voiceInstancesContainer.VoiceInstances.TryGetValue(id, out var instance)
            || instance is null)
        {
            return NotFound(new 
            {
                key = "bot",
                message = "Bot is not in a voice channel in this guild."
            });
        }

        if (!instance.StopPlaying())
            return NotFound(new 
            {
                key = "stream",
                error = "Nothing is playing in this guild." 
            });

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
