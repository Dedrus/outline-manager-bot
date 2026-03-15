using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TgBotVPN.Data;
using TgBotVPN.Models;

namespace TgBotVPN.Services;

public class DatabaseService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AdminValidationService _adminValidationService;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(AdminValidationService adminValidationService,
        ILogger<DatabaseService> logger, IServiceScopeFactory scopeFactory)
    {
        _adminValidationService = adminValidationService;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<TelegramUser> GetOrCreateUserAsync(long telegramId, string username)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.TelegramUsers.FindAsync(telegramId);
        if (user != null)
        {
            _logger.LogInformation("User found: {TelegramId}", telegramId);
            return user;
        }

        var isAdmin = _adminValidationService.IsAdmin(telegramId);
        user = new TelegramUser
        {
            TelegramId = telegramId,
            Username = username,
            IsWhitelisted = isAdmin,
            IsAdmin = isAdmin,
            CreatedAt = DateTime.UtcNow
        };

        context.TelegramUsers.Add(user);
        await context.SaveChangesAsync();
        _logger.LogInformation("User created: {TelegramId} ({Username}) - IsAdmin: {IsAdmin}", telegramId, username,
            isAdmin);
        return user;
    }

    public async Task<TelegramUser?> GetUserAsync(long telegramId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.TelegramUsers.FindAsync(telegramId);
    }

    public async Task<bool> IsUserWhitelistedAsync(long telegramId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.TelegramUsers.FindAsync(telegramId);
        return user?.IsWhitelisted ?? false;
    }

    public async Task<bool> IsUserAdminAsync(long telegramId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.TelegramUsers.FindAsync(telegramId);
        return user?.IsAdmin ?? false;
    }

    public async Task AddUserToWhitelistAsync(long telegramId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.TelegramUsers.FindAsync(telegramId);
        if (user != null)
        {
            user.IsWhitelisted = true;
            await context.SaveChangesAsync();
            _logger.LogInformation("User added to whitelist: {TelegramId}", telegramId);
        }
    }

    public async Task RemoveUserFromWhitelistAsync(long telegramId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.TelegramUsers.FindAsync(telegramId);
        if (user != null)
        {
            user.IsWhitelisted = false;
            _logger.LogInformation("User removed from whitelist: {TelegramId}", telegramId);
        }

        var outLineKey = await context.OutlineKeys.Where(c => c.TelegramId == telegramId).FirstOrDefaultAsync();
        if (outLineKey is not null)
        {
            context.OutlineKeys.Remove(outLineKey);
            _logger.LogInformation("Outline key deleted for user: {TelegramId}", telegramId);
        }
        
        await context.SaveChangesAsync();
    }

    public async Task<OutlineKey> CreateKeyAsync(long telegramId, string keyId, string keyName, string accessUrl,
        int dataLimitGb)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = new OutlineKey
        {
            TelegramId = telegramId,
            KeyId = keyId,
            KeyName = keyName,
            AccessUrl = accessUrl,
            DataLimitGb = dataLimitGb,
            LastUpdated = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        context.OutlineKeys.Add(key);
        await context.SaveChangesAsync();
        _logger.LogInformation("Key created for user {TelegramId}: {KeyName}", telegramId, keyName);
        return key;
    }

    public async Task<OutlineKey?> GetUserKeyAsync(long telegramId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.OutlineKeys.FirstOrDefaultAsync(k => k.TelegramId == telegramId);
    }

    public async Task<List<OutlineKey>> GetKeysNeedingUpdateAsync(int updateIntervalDays)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoffDate = DateTime.UtcNow.AddDays(-updateIntervalDays);
        return await context.OutlineKeys
            .Where(k => k.LastUpdated < cutoffDate)
            .ToListAsync();
    }

    public async Task UpdateKeyDataLimitAsync(long telegramId, int dataLimitGb)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = await context.OutlineKeys.FirstOrDefaultAsync(k => k.TelegramId == telegramId);
        if (key != null)
        {
            key.DataLimitGb = dataLimitGb;
            key.LastUpdated = DateTime.UtcNow;
            await context.SaveChangesAsync();
            _logger.LogInformation("Key updated for user {TelegramId}: new limit {DataLimit} GB", telegramId,
                dataLimitGb);
        }
    }

    public async Task UpdateKeyLastUpdatedAsync(long telegramId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = await context.OutlineKeys.FirstOrDefaultAsync(k => k.TelegramId == telegramId);
        if (key != null)
        {
            key.LastUpdated = DateTime.UtcNow;
            await context.SaveChangesAsync();
            _logger.LogInformation("Key LastUpdated refreshed for user {TelegramId}", telegramId);
        }
    }

    public async Task<List<TelegramUser>> GetPendingUsersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.TelegramUsers
            .Where(u => !u.IsWhitelisted)
            .ToListAsync();
    }

    public async Task<List<OutlineKey>> GetAllKeysWithUsersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.OutlineKeys.Include(x => x.TelegramUser).ToListAsync();
    }

    public async Task<List<TelegramUser>> GetAllWhiteListUsersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.TelegramUsers.Where(c => c.IsWhitelisted).ToListAsync();
    }
}