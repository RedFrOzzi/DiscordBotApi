using Serilog;

namespace DiscordBotApi.Services.GuildUpdater;

public sealed class GuildUpdateBackgroundService : BackgroundService
{
    private readonly GuildUpdateQueue _queue;
    private readonly IServiceScopeFactory _scopes;

    public GuildUpdateBackgroundService(
        GuildUpdateQueue queue,
        IServiceScopeFactory scopes)
    {
        _queue = queue;
        _scopes = scopes;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IGuildUpdateProcessor>();
                await processor.ProcessAsync(item.GuildId, stoppingToken);
                Log.Information("Guild update done for {GuildId} ({JobId})", item.GuildId, item.Status.JobId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                Log.Error(ex, "Guild update failed for {GuildId}", item.GuildId);
            }
            finally
            {
                _queue.Complete(item);
            }
        }
    }
}