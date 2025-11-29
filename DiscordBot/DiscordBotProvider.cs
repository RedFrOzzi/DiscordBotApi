using DiscordBotApi.DiscordBot.Services.Secrets;
using NetCord;
using NetCord.Gateway;
using NetCord.Logging;
using System.Text.Json;

namespace DiscordBotApi.DiscordBot
{
    public class DiscordBotProvider
    {
        public static GatewayClient CreateBotClient([FromKeyedServices("writer")] StreamWriter textWriter)
        {
            Console.WriteLine("Initializing discord bot");

            string token;

            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/secrets.txt"))
            {
                throw new Exception($"secrets not found in base directory {AppDomain.CurrentDomain.BaseDirectory}");
            }
            else
            {
                var secretsJson = JsonSerializer.Deserialize<SecretsJson>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/secrets.txt"))
                    ?? throw new Exception($"could not deserialize json file");

                token = secretsJson.Token ?? throw new Exception($"secrets file does not contain token");
            }

            textWriter.WriteLine($"{DateTime.UtcNow}: Starting bot");

            GatewayClient client = new(new BotToken(token), new GatewayClientConfiguration
            {
                Intents = GatewayIntents.All,
                Logger = new TextWriterLogger(textWriter, minimumLogLevel: NetCord.Logging.LogLevel.Error)
            });

            return client;
        }
    }
}