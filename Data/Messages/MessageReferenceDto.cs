using NetCord;
using System.Text.Json.Serialization;

namespace DiscordBotApi.Data.Messages;

public class MessageReferenceDto
{
    [JsonPropertyName("message_id")]
    public ulong MessageId { get; set; }

    [JsonPropertyName("channel_id")]
    public ulong ChannelId { get; set; }
}
