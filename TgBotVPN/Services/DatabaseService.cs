using Microsoft.EntityFrameworkCore;
using Serilog;
using TgBotVPN.Data;
using TgBotVPN.Models;

namespace TgBotVPN.Services;

public class DatabaseService
{
    private readonly AppDbContext _context;
    private readonly long _adminTelegramId;
    private readonly ILogger _logger;

    public DatabaseService(AppDbContext context, long adminTelegramId)
    {
        _context = context;
        _adminTelegramId = adminTelegramId;
        _logger = Log.ForContext<DatabaseService>();
    }

    public async Task<TelegramUser> GetOrCreateUserAsync(long telegramId, string username)
    {
        var user = await _context.TelegramUsers.FindAsync(telegramId);
        if (user != null)
        {
            _logger.Information("User found: {TelegramId}", telegramId);
            return user;
        }

        var isAdmin = telegramId == _adminTelegramId;
        user = new TelegramUser
        {
            TelegramId = telegramId,
            Username = username,
            IsWhitelisted = isAdmin,  // Admin is automatically whitelisted
            IsAdmin = isAdmin,
            CreatedAt = DateTime.UtcNow
        };

        _context.TelegramUsers.Add(user);
        await _context.SaveChangesAsync();
        _logger.Information("User created: {TelegramId} ({Username}) - IsAdmin: {IsAdmin}", telegramId, username, isAdmin);
        return user;
    }

    public async Task<TelegramUser?> GetUserAsync(long telegramId)
    {
        return await _context.TelegramUsers.FindAsync(telegramId);
    }

    public async Task<bool> IsUserWhitelistedAsync(long telegramId)
    {
        var user = await _context.TelegramUsers.FindAsync(telegramId);
        return user?.IsWhitelisted ?? false;
    }

    public async Task<bool> IsUserAdminAsync(long telegramId)
    {
        var user = await _context.TelegramUsers.FindAsync(telegramId);
        return user?.IsAdmin ?? false;
    }

    public async Task AddUserToWhitelistAsync(long telegramId)
    {
        var user = await _context.TelegramUsers.FindAsync(telegramId);
        if (user != null)
        {
            user.IsWhitelisted = true;
            await _context.SaveChangesAsync();
            _logger.Information("User added to whitelist: {TelegramId}", telegramId);
        }
    }

    public async Task RemoveUserFromWhitelistAsync(long telegramId)
    {
        var user = await _context.TelegramUsers.FindAsync(telegramId);
        if (user != null)
        {
            user.IsWhitelisted = false;
            await _context.SaveChangesAsync();
            _logger.Information("User removed from whitelist: {TelegramId}", telegramId);
        }
    }

    public async Task<OutlineKey> CreateKeyAsync(long telegramId, string keyId, string keyName, string accessUrl, int dataLimitGb)
    {
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

        _context.OutlineKeys.Add(key);
        await _context.SaveChangesAsync();
        _logger.Information("Key created for user {TelegramId}: {KeyName}", telegramId, keyName);
        return key;
    }

    public async Task<OutlineKey?> GetUserKeyAsync(long telegramId)
    {
        return await _context.OutlineKeys.FirstOrDefaultAsync(k => k.TelegramId == telegramId);
    }

    public async Task<List<OutlineKey>> GetKeysNeedingUpdateAsync(int updateIntervalDays)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-updateIntervalDays);
        return await _context.OutlineKeys
            .Where(k => k.LastUpdated < cutoffDate)
            .ToListAsync();
    }

    public async Task UpdateKeyDataLimitAsync(long telegramId, int dataLimitGb)
    {
        var key = await _context.OutlineKeys.FirstOrDefaultAsync(k => k.TelegramId == telegramId);
        if (key != null)
        {
            key.DataLimitGb = dataLimitGb;
            key.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.Information("Key updated for user {TelegramId}: new limit {DataLimit} GB", telegramId, dataLimitGb);
        }
    }

    public async Task UpdateKeyLastUpdatedAsync(long telegramId)
    {
        var key = await _context.OutlineKeys.FirstOrDefaultAsync(k => k.TelegramId == telegramId);
        if (key != null)
        {
            key.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.Information("Key LastUpdated refreshed for user {TelegramId}", telegramId);
        }
    }

    public async Task<List<TelegramUser>> GetPendingUsersAsync()
    {
        return await _context.TelegramUsers
            .Where(u => !u.IsWhitelisted)
            .ToListAsync();
    }

    public async Task<List<OutlineKey>> GetAllKeysAsync()
    {
        return await _context.OutlineKeys.ToListAsync();
    }

    public async Task<List<TelegramUser>> GetAllWhiteListUsersAsync()
    {
        return await _context.TelegramUsers.Where(c => c.IsWhitelisted).ToListAsync();
    }
}
