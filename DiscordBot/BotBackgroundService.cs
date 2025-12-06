using DiscordBotApi.Database;
using DiscordBotApi.Utilities;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace DiscordBotApi.DiscordBot
{
    public class BotBackgroundService : BackgroundService
    {
        readonly GatewayClient _client;
        readonly StreamWriter _textWriter;
        readonly IServiceProvider _serviceProvider;

        public BotBackgroundService([FromKeyedServices("client")] GatewayClient client, [FromKeyedServices("writer")] StreamWriter textWriter, IServiceProvider serviceProvider)
        {
            _client = client;
            _textWriter = textWriter;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ApplicationCommandService<ApplicationCommandContext> applicationCommandService = new();
            ComponentInteractionService<ModalInteractionContext> modalInteractionService = new();
            ComponentInteractionService<ButtonInteractionContext> buttonInteractionService = new();

            applicationCommandService.AddModules(typeof(BotBackgroundService).Assembly);
            modalInteractionService.AddModules(typeof(BotBackgroundService).Assembly);
            buttonInteractionService.AddModules(typeof(BotBackgroundService).Assembly);

            applicationCommandService.AddSlashCommand(new SlashCommandBuilder("кто", "Кто такая Ольга Николаевна?", () => "https://vk.com/video132462861_163282764"));

            _textWriter.WriteLine($"{DateTime.UtcNow}: Slash command added");

            _client.InteractionCreate += async interaction =>
            {
                if (interaction is not ApplicationCommandInteraction applicationCommandInteraction)
                    return;

                var result = await applicationCommandService.ExecuteAsync(new ApplicationCommandContext(applicationCommandInteraction, _client), _serviceProvider);

                if (result is not IFailResult failResult)
                    return;

                try
                {
                    _textWriter.WriteLine($"{failResult.Message}");

                    await interaction.SendResponseAsync(InteractionCallback.Message(new()
                    {
                        Content = "Ничего не вышло. Ты все сломал, хватит уже",
                        Flags = MessageFlags.Ephemeral,
                    }));
                }
                catch { }
            };

            _client.InteractionCreate += async interaction =>
            {
                if (interaction is not ButtonInteraction buttonInteraction)
                    return;

                var result = await buttonInteractionService.ExecuteAsync(new ButtonInteractionContext(buttonInteraction, _client), _serviceProvider);

                if (result is not IFailResult failResult)
                    return;

                try
                {
                    _textWriter.WriteLine($"{failResult.Message}");

                    await interaction.SendResponseAsync(InteractionCallback.Message(new()
                    {
                        Content = "Ничего не вышло. Ты все сломал, хватит уже",
                        Flags = MessageFlags.Ephemeral,
                    }));
                }
                catch { }
            };

            _client.InteractionCreate += async interaction =>
            {
                if (interaction is not ModalInteraction modalInteraction)
                    return;

                var result = await modalInteractionService.ExecuteAsync(new ModalInteractionContext(modalInteraction, _client), _serviceProvider);

                if (result is not IFailResult failResult)
                    return;

                try
                {
                    _textWriter.WriteLine($"{failResult.Message}");

                    await interaction.SendResponseAsync(InteractionCallback.Message(new()
                    {
                        Content = "Ничего не вышло. Ты все сломал, хватит уже",
                        Flags = MessageFlags.Ephemeral,
                    }));
                }
                catch { }
            };

            await applicationCommandService.RegisterCommandsAsync(_client.Rest, _client.Id, cancellationToken: stoppingToken);

            _textWriter.WriteLine($"{DateTime.UtcNow}: Slash command registred");

            //Close raffle when message is deleted in discord
            _client.MessageDelete += args =>
            {
                RaffleUtils.CloseRaffle(_serviceProvider, args.MessageId);
                return default;
            };

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
