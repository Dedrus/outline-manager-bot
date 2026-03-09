using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using TgBotVPN.Configuration;
using TgBotVPN.Data;

namespace TgBotVPN.Services;

public class KeyUpdateService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly int _checkIntervalSeconds;
    private readonly int _updateIntervalDays;
    private readonly ILogger _logger;

    public KeyUpdateService(IServiceProvider serviceProvider, IOptions<KeyUpdateServiceSettings> options)
    {
        _serviceProvider = serviceProvider;
        var settings = options.Value;
        _checkIntervalSeconds = settings.CheckIntervalSeconds;
        _updateIntervalDays = settings.UpdateIntervalDays;
        _logger = Log.ForContext<KeyUpdateService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("KeyUpdateService started. Check interval: {Seconds}s, Update interval: {Days} days",
            _checkIntervalSeconds, _updateIntervalDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndUpdateKeysAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in KeyUpdateService");
            }

            await Task.Delay(_checkIntervalSeconds * 1000, stoppingToken);
        }

        _logger.Information("KeyUpdateService stopped");
    }

    private async Task CheckAndUpdateKeysAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var outlineService = scope.ServiceProvider.GetRequiredService<OutlineApiService>();

        var keysToUpdate = await dbService.GetKeysNeedingUpdateAsync(_updateIntervalDays);

        if (keysToUpdate.Count == 0)
        {
            return;
        }

        _logger.Information("Found {Count} keys to update", keysToUpdate.Count);

        foreach (var key in keysToUpdate)
        {
            try
            {
                // Update data limit on Outline API
                var success = await outlineService.UpdateKeyDataLimitAsync(key.KeyId, key.DataLimitGb);

                if (success)
                {
                    // Update LastUpdated in database
                    await dbService.UpdateKeyLastUpdatedAsync(key.TelegramId);
                    _logger.Information("Key {KeyName} (TelegramId: {TelegramId}) updated successfully", 
                        key.KeyName, key.TelegramId);
                }
                else
                {
                    _logger.Warning("Failed to update key {KeyName} (TelegramId: {TelegramId})", 
                        key.KeyName, key.TelegramId);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating key {KeyName} (TelegramId: {TelegramId})", 
                    key.KeyName, key.TelegramId);
            }
        }
    }
}
