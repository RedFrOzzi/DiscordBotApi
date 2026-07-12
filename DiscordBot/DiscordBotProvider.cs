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
            Console.WriteLine($"{DateTime.UtcNow}: Initializing discord bot");

            string token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN") ?? throw new("Discord token was null");

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