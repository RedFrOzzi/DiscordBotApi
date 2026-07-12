using NetCord;
using NetCord.Rest;
using System.Text.Json.Serialization;

namespace DiscordBotApi.Data.Messages
{
    public class SendEmbedDto
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("timestamp")]
        public DateTimeOffset? Timestamp { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("color")]
        public Color Color { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("footer")]
        public EmbedFooterProperties? Footer { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("image")]
        public EmbedImageProperties? Image { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("thumbnail")]
        public EmbedThumbnailProperties? Thumbnail { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("author")]
        public EmbedAuthorProperties? Author { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("fields")]
        public IEnumerable<EmbedFieldProperties>? Fields { get; set; }


        public EmbedProperties Convert()
        {
            return new()
            {
                Title = Title,
                Description = Description,
                Url = Url,
                Timestamp = Timestamp,
                Color = Color,
                Footer = Footer,
                Image = Image,
                Thumbnail = Thumbnail,
                Author = Author,
                Fields = Fields,
            };
        }
    }
}
