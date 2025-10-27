using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Services.ComponentInteractions;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using DiscordBotApi.DiscordBot.Services.Secrets;
using NetCord.Rest;

namespace DiscordBotApi.DiscordBot
{
    public class DiscordBotBackgroundService : BackgroundService
    {
        public RestClient? Client => _host?.Services.GetService<RestClient>();
        IHost? _host;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var secrets = SecretsLoader.GetSecrets();

            var builder = Host.CreateApplicationBuilder();

            builder.Services
                .AddDiscordGateway(config =>
                {
                    config.Token = secrets.Token;
                    config.Intents = NetCord.Gateway.GatewayIntents.All;
                })
                .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
                .AddComponentInteractions<ModalInteraction, ModalInteractionContext>()
                .AddApplicationCommands()
                .AddGatewayHandlers(typeof(Program).Assembly);

            _host = builder.Build();

            _host.AddModules(typeof(Program).Assembly);

            _host.UseGatewayHandlers();

            return _host.RunAsync(stoppingToken);
        }
    }
}
