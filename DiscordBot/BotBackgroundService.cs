using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace DiscordBotApi.DiscordBot
{
    public class BotBackgroundService : BackgroundService
    {
        readonly GatewayClient _client;
        readonly StreamWriter _textWriter;

        public BotBackgroundService([FromKeyedServices("client")] GatewayClient client, [FromKeyedServices("writer")] StreamWriter textWriter)
        {
            _client = client;
            _textWriter = textWriter;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ApplicationCommandService<ApplicationCommandContext> applicationCommandService = new();

            applicationCommandService.AddSlashCommand(new SlashCommandBuilder("ping", "Ping!", () => "Pong!"));

            _textWriter.WriteLine($"{DateTime.UtcNow}: Slash command added");

            _client.InteractionCreate += async interaction =>
            {
                if (interaction is not ApplicationCommandInteraction applicationCommandInteraction)
                    return;

                var result = await applicationCommandService.ExecuteAsync(new ApplicationCommandContext(applicationCommandInteraction, _client));

                if (result is not IFailResult failResult)
                    return;

                try
                {
                    await interaction.SendResponseAsync(InteractionCallback.Message(failResult.Message));
                }
                catch
                {
                }
            };

            await applicationCommandService.RegisterCommandsAsync(_client.Rest, _client.Id, cancellationToken: stoppingToken);

            _textWriter.WriteLine($"{DateTime.UtcNow}: Slash command registred");

            await _client.StartAsync(cancellationToken: stoppingToken);

            _textWriter.WriteLine($"{DateTime.UtcNow}: Client started");
            Console.WriteLine("Client started");

            await Task.Delay(-1, stoppingToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _textWriter.WriteLine($"{DateTime.UtcNow}: Client was shut down");

            _client.Dispose();
            _textWriter.Dispose();

            return base.StopAsync(cancellationToken);
        }
    }
}
