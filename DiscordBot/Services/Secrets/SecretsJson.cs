namespace DiscordBotApi.DiscordBot.Services.Secrets
{
    [Serializable]
    public class SecretsJson
    {
        public string? Token { get; set; }
        public string? Salt { get; set; }
    }
}
