using DiscordBotApi.Database;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DiscordBotApi.Services;

public class AudioFilesCleanupService(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    readonly IConfiguration _configuration = configuration;
    readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    readonly string _directory = Path.Combine(Path.Combine(AppContext.BaseDirectory, "DataStorage"), "TempAudio");

    const int _defaultHourUtc = 8;
    const int _defaultMaxAgeHours = 12;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hourUtc = _configuration.GetValue<int?>("AudioFilesCleanup:HourUtc") ?? _defaultHourUtc;
        var maxAgeHours = _configuration.GetValue<int?>("AudioFilesCleanup:MaxAgeHours") ?? _defaultMaxAgeHours;
        

        if (string.IsNullOrWhiteSpace(_directory))
        {
            Log.Warning("AudioFilesCleanup: no directory configured, service disabled.");
            return;
        }

        Log.Information(
            "AudioFilesCleanup started. Target: {Directory}, hour UTC: {Hour}, max age: {MaxAge}h",
            _directory, hourUtc, maxAgeHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun(hourUtc);

            Log.Information("Next cleanup scheduled in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                RunCleanup(_serviceScopeFactory, _directory, maxAgeHours);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AudioFilesCleanup: cleanup run failed");
            }
        }
    }

    private static TimeSpan GetDelayUntilNextRun(int hourUtc)
    {
        var now = DateTime.UtcNow;
        var next = now.Date.AddHours(hourUtc);

        if (next <= now)
            next = next.AddDays(1);

        return next - now;
    }

    private static void RunCleanup(IServiceScopeFactory scopeFactory, string directory, int maxAgeHours)
    {
        if (!Directory.Exists(directory))
        {
            Log.Warning("AudioFilesCleanup: directory not found: {Directory}", directory);
            return;
        }

        var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateTime timeMark = DateTime.UtcNow.AddHours(-12);
        var states = dbContext.AudioExtractionState
            .Where(s => s.Status != "pending"
            && s.CreatedAt > timeMark)
            .ExecuteDelete();

        dbContext.SaveChanges();

        var cutoff = DateTime.UtcNow.AddHours(-maxAgeHours);
        var deleted = 0;
        var skipped = 0;

        foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*.mp3", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (file.CreationTimeUtc > cutoff)
                    continue;

                file.Delete();
                deleted++;
                Log.Information("Deleted old audio file: {File} (age {Age:F1}h)",
                    file.Name, (DateTime.UtcNow - file.CreationTimeUtc).TotalHours);
            }
            catch (IOException ex)
            {
                skipped++;
                Log.Information("Skipped locked file {File}: {Message}", file.Name, ex.Message);
            }
            catch (Exception ex)
            {
                skipped++;
                Log.Warning(ex, "Failed to delete {File}", file.Name);
            }
        }

        Log.Information("AudioFilesCleanup: run complete. Deleted: {Deleted}, skipped: {Skipped}",
            deleted, skipped);
    }
}
