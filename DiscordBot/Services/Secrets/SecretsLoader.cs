using System.Text.Json;

namespace DiscordBotApi.DiscordBot.Services.Secrets
{
    public static class SecretsLoader
    {
        public static bool TryGetSecrets(out SecretsJson secrets)
        {
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/secrets.txt"))
            {
                secrets = new SecretsJson();
                return false;
            }
            else
            {
                secrets = JsonSerializer.Deserialize<SecretsJson>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/secrets.txt"))!;
                if (secrets == null)
                {
                    secrets = new SecretsJson();
                    return false;
                }

                return true;
            }
        }
    }
}
