using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.AudioExtractionStates;

public class AudioExtractionState
{
    [Key] public int Key { get; set; }
    [Required] public Guid RequestId { get; set; }
    [Required] public string Status { get; set; } = string.Empty; //Could be: "pending" | "succeeded" | "failed"
    public string? Name { get; set; }
    public string? ResultMessage { get; set; }
    public DateTime? CreatedAt { get; set; }
}
