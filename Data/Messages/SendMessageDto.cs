using NetCord;
using NetCord.Rest;
using System.Text.Json.Serialization;
using NetCord.JsonModels;

namespace DiscordBotApi.Data.Messages;

public class SendMessageDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("tts")]
    public bool Tts { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("message_reference")]
    public MessageReferenceDto? MessageReference { get; set; }


    public MessageProperties ConvertoToMessageProperties()
    {
        MessageReferenceProperties? mRefProps = null;
        if (MessageReference != null 
            && MessageReference.MessageId != default
            && MessageReference.ChannelId != default)
        {
            mRefProps = MessageReferenceProperties.Reply(MessageReference.MessageId, false);
            mRefProps.ChannelId = MessageReference.ChannelId;
        }

        MessageProperties properties = new()
        {
            Content = Content,
            Tts = Tts,
            MessageReference = mRefProps
        };

        return properties;
    }
}
