using DiscordBotApi.Data.Guilds;
using System.ComponentModel.DataAnnotations;

namespace DiscordBotApi.Data.AudioTracks;

public class AudioTrack
{
    [Key] public int Key { get; set; }
    [Required] public string Title { get; set; } = string.Empty;
    [Required] public string Path { get; set; } = string.Empty;
    [Required] public DiscordGuild Guild { get; set; } = null!;
    public double? SizeInKb { get; set; }
    public DateTime? CreatedAt { get; set; }

    public void SetSize(long sizeInBytes) => SizeInKb = sizeInBytes / 1024.0;
}
