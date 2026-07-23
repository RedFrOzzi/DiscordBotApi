using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.BotFeatures.RaffleService;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace DiscordBotApi.DiscordBot.BotFeatures.MessageHandlers;

public class MessageDeleteHandler(IServiceScopeFactory factory) : IMessageDeleteGatewayHandler
{
    private readonly IServiceScopeFactory _factory = factory;

    public ValueTask HandleAsync(MessageDeleteEventArgs args)
    {
        using var scope = _factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        RaffleUtils.TryCloseRaffleOnMessageDeletion(dbContext, args.MessageId);
        return default;
    }
}
