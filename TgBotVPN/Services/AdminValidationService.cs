using Microsoft.Extensions.Options;
using TgBotVPN.Configuration;

namespace TgBotVPN.Services;

public class AdminValidationService
{
    private readonly long[] _adminTelegramIds;

    public AdminValidationService(IOptions<TelegramBotSettings> options)
    {
        _adminTelegramIds = options.Value.AdminTelegramIds;
    }

    public bool IsAdmin(long telegramId) => _adminTelegramIds.Contains(telegramId);
}