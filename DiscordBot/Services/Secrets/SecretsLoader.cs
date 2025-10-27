namespace DiscordBotApi.DiscordBot.Services.Secrets
{
    public static class SecretsLoader
    {
        public static Secrets GetSecrets()
        {
            var path = AppDomain.CurrentDomain.BaseDirectory + "secrets.json";
            if (File.Exists(path))
            {
                var scrts = System.Text.Json.JsonSerializer.Deserialize<Secrets>(File.ReadAllText(path));
                if (scrts == null || string.IsNullOrEmpty(scrts.Token))
                {
                    throw new Exception("Secrets are empty");
                }

                return scrts;
            }

            throw new Exception("Secrets file does not exist");
        }

        public class Secrets
        {
            public string Token { get; set; } = string.Empty;
            public List<ulong> AdminIds { get; set; } = [];
        }
    }
}
