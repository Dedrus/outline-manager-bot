using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Telegram.Bot;
using TgBotVPN.Configuration;

namespace TgBotVPN.Services;

public class KeyUpdateService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval;
    private readonly int _updateIntervalDays;
    private readonly ILogger _logger;
    private readonly ITelegramBotClient _botClient;

    public KeyUpdateService(IServiceProvider serviceProvider, IOptions<KeyUpdateServiceSettings> options,
        ITelegramBotClient botClient)
    {
        _serviceProvider = serviceProvider;
        _botClient = botClient;
        var settings = options.Value;
        _checkInterval = settings.CheckInterval;
        _updateIntervalDays = settings.UpdateIntervalDays;
        _logger = Log.ForContext<KeyUpdateService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("KeyUpdateService started. Check interval: {CheckInterval}, Update interval: {Days} days",
            _checkInterval, _updateIntervalDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndUpdateKeysAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in KeyUpdateService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.Information("KeyUpdateService stopped");
    }

    private async Task CheckAndUpdateKeysAsync(CancellationToken stoppingToken)
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
            bool success = false;
            try
            {
                // Update data limit on Outline API
                success = await outlineService.UpdateKeyDataLimitAsync(key.KeyId, key.DataLimitGb);

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

            if (!success)
            {
                continue;
            }
            try
            {
                await _botClient.SendTextMessageAsync(key.TelegramId, $"Ваш лимит трафика на 30 дней был автоматически обновлен. Текущий лимит {key.DataLimitGb} ГБ.", cancellationToken: stoppingToken);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error sending push message about key update {KeyName} (TelegramId: {TelegramId})",
                    key.KeyName, key.TelegramId);
            }
        }
    }
}