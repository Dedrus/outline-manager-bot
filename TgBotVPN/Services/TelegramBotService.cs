using Microsoft.Extensions.Options;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgBotVPN.Configuration;

namespace TgBotVPN.Services;

public class TelegramBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly DatabaseService _dbService;
    private readonly OutlineApiService _outlineService;
    private readonly long _adminTelegramId;
    private readonly int _defaultDataLimitGb;
    private readonly ILogger _logger;

    public TelegramBotService(
        IOptions<TelegramBotSettings> botSettings,
        IOptions<DatabaseSettings> dbSettings,
        DatabaseService dbService,
        OutlineApiService outlineService)
    {
        var botOpts = botSettings.Value;
        var dbOpts = dbSettings.Value;
        
        var token = botOpts.Token ?? throw new InvalidOperationException("Bot token not configured");
        _adminTelegramId = botOpts.AdminTelegramId;
        _defaultDataLimitGb = dbOpts.DefaultDataLimitGb;

        _botClient = new TelegramBotClient(token);
        _dbService = dbService;
        _outlineService = outlineService;
        _logger = Log.ForContext<TelegramBotService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        _logger.Information("Telegram bot started");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token);

        var me = await _botClient.GetMeAsync(cancellationToken);
        _logger.Information("Bot logged in as {BotUsername}", me.Username);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message?.Type != MessageType.Text)
            return;

        var message = update.Message;
        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? 0;
        if (userId == 0) return;

        var username = message.From?.Username ?? message.From?.FirstName ?? "Unknown";
        var text = message.Text ?? string.Empty;

        _logger.Information("Received message from {UserId} ({Username}): {Text}", userId, username, text);

        try
        {
            // Register user if not exists
            await _dbService.GetOrCreateUserAsync(userId, username);

            var isAdmin = await _dbService.IsUserAdminAsync(userId);

            // Reply-menu buttons (they arrive as plain text messages)
            if (text == "🔑 Мой ключ")
            {
                await HandleGetKeyAsync(botClient, chatId, userId, cancellationToken);
            }
            else if (text == "➕ Создать ключ")
            {
                await HandleCreateKeyAsync(botClient, chatId, userId, username, cancellationToken);
            }
            else if (text == "❓ Помощь")
            {
                await HandleHelpAsync(botClient, chatId, userId, cancellationToken);
            }
            else if (isAdmin && text == "👥 На одобрение")
            {
                await HandleAdminPendingUsersAsync(botClient, chatId, userId, cancellationToken);
            }
            else if (isAdmin && text == "🗝 Все ключи")
            {
                await HandleAdminAllKeysAsync(botClient, chatId, userId, cancellationToken);
            }
            else if (text.StartsWith("/start"))
            {
                await HandleStartAsync(botClient, chatId, userId, cancellationToken);
            }
            else if (text.StartsWith("/create_key"))
            {
                await HandleCreateKeyAsync(botClient, chatId, userId, username, cancellationToken);
            }
            else if (text.StartsWith("/my_key"))
            {
                await HandleGetKeyAsync(botClient, chatId, userId, cancellationToken);
            }
            else if (text.StartsWith("/help"))
            {
                await HandleHelpAsync(botClient, chatId, userId, cancellationToken);
            }
            else if (text.StartsWith("/admin_add_user"))
            {
                await HandleAdminAddUserAsync(botClient, chatId, userId, text, cancellationToken);
            }
            else if (text.StartsWith("/admin_remove_user"))
            {
                await HandleAdminRemoveUserAsync(botClient, chatId, userId, text, cancellationToken);
            }
            else if (text.StartsWith("/admin_set_limit"))
            {
                await HandleAdminSetLimitAsync(botClient, chatId, userId, text, cancellationToken);
            }
            else if (text.StartsWith("/admin_pending_users"))
            {
                await HandleAdminPendingUsersAsync(botClient, chatId, userId, cancellationToken);
            }
            else if (text.StartsWith("/admin_all_keys"))
            {
                await HandleAdminAllKeysAsync(botClient, chatId, userId, cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "❓ Неизвестная команда. Введите /help для списка доступных команд.", cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling message from {UserId}", userId);
            await botClient.SendTextMessageAsync(chatId, "❌ Произошла ошибка. Пожалуйста, попробуйте позже.", cancellationToken: cancellationToken);
        }
    }

    private async Task HandleStartAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        var isAdmin = await _dbService.IsUserAdminAsync(userId);
        var message = "👋 Добро пожаловать в Outline VPN бота!\n\n" +
                      "Вы зарегистрированы. Пожалуйста, ожидайте одобрения администратора.\n\n" +
                      "Используйте меню снизу или введите /help для просмотра доступных команд.";

        await botClient.SendTextMessageAsync(chatId, message, replyMarkup: GetMenuKeyboard(isAdmin), cancellationToken: cancellationToken);
    }

    private async Task HandleCreateKeyAsync(ITelegramBotClient botClient, long chatId, long userId, string username, CancellationToken cancellationToken)
    {
        var isWhitelisted = await _dbService.IsUserWhitelistedAsync(userId);
        if (!isWhitelisted)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Вы не в списке разрешенных. Пожалуйста, ожидайте одобрения администратора.", cancellationToken: cancellationToken);
            return;
        }

        var existingKey = await _dbService.GetUserKeyAsync(userId);
        if (existingKey != null)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ У вас уже есть ключ. Используйте /my_key для его получения.", cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendTextMessageAsync(chatId, "⏳ Создаю ваш ключ...", cancellationToken: cancellationToken);

        try
        {
            var accessKey = await _outlineService.CreateKeyAsync(username, _defaultDataLimitGb);
            await _dbService.CreateKeyAsync(userId, accessKey.Id, accessKey.Name, accessKey.AccessUrl, _defaultDataLimitGb);

            var keyMessage = $"✅ Ключ успешно создан!\n\n" +
                           $"Ваш лимит данных: {_defaultDataLimitGb} ГБ\n\n" +
                           $"Ссылка доступа:\n`{accessKey.AccessUrl}`";
            await botClient.SendTextMessageAsync(chatId, keyMessage, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            _logger.Information("Key created for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create key for user {UserId}", userId);
            await botClient.SendTextMessageAsync(chatId, "❌ Не удалось создать ключ. Пожалуйста, попробуйте позже.", cancellationToken: cancellationToken);
        }
    }

    private async Task HandleGetKeyAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        var isWhitelisted = await _dbService.IsUserWhitelistedAsync(userId);
        if (!isWhitelisted)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Вы не в списке разрешенных.", cancellationToken: cancellationToken);
            return;
        }

        var key = await _dbService.GetUserKeyAsync(userId);
        if (key == null)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ У вас еще нет ключа. Используйте /create_key для создания.", cancellationToken: cancellationToken);
            return;
        }

        var keyMessage = $"🔑 Ваш ключ\n\n" +
                        $"Имя: {key.KeyName}\n" +
                        $"Лимит данных: {key.DataLimitGb} ГБ\n" +
                        $"Последнее обновление: {key.LastUpdated:yyyy-MM-dd HH:mm:ss} UTC\n\n" +
                        $"Ссылка доступа:\n`{key.AccessUrl}`";
        await botClient.SendTextMessageAsync(chatId, keyMessage, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
    }

    private async Task HandleHelpAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        var isAdmin = await _dbService.IsUserAdminAsync(userId);

        var helpMessage = "📚 Доступные команды\n\n" +
                          "/start - Зарегистрироваться в боте\n" +
                          "/create_key - Создать новый VPN ключ\n" +
                          "/my_key - Получить текущий ключ\n" +
                          "/help - Показать эту справку\n";

        if (isAdmin)
        {
            helpMessage += "\n👨‍💼 Админ-команды\n" +
                           "/admin_add_user <telegram_id> - Добавить пользователя\n" +
                           "/admin_remove_user <telegram_id> - Удалить пользователя\n" +
                           "/admin_set_limit <telegram_id> <limit_gb> - Установить лимит\n" +
                           "/admin_pending_users - Список на одобрение\n" +
                           "/admin_all_keys - Список всех ключей";
        }

        await botClient.SendTextMessageAsync(chatId, helpMessage, replyMarkup: GetMenuKeyboard(isAdmin), cancellationToken: cancellationToken);
    }

    private async Task HandleAdminAddUserAsync(ITelegramBotClient botClient, long chatId, long userId, string text, CancellationToken cancellationToken)
    {
        if (userId != _adminTelegramId)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.", cancellationToken: cancellationToken);
            return;
        }

        var parts = text.Split();
        if (parts.Length < 2 || !long.TryParse(parts[1], out var targetUserId))
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Неверный формат. Используйте: /admin_add_user <telegram_id>", cancellationToken: cancellationToken);
            return;
        }

        var user = await _dbService.GetUserAsync(targetUserId);
        if (user == null)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Пользователь не найден.", cancellationToken: cancellationToken);
            return;
        }

        await _dbService.AddUserToWhitelistAsync(targetUserId);
        var message = $"✅ Пользователь `{user.Username}` (ID: `{user.TelegramId}`) добавлен в список разрешенных.";
        await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
        _logger.Information("Admin {AdminId} added user {UserId} to whitelist", userId, targetUserId);
    }

    private async Task HandleAdminRemoveUserAsync(ITelegramBotClient botClient, long chatId, long userId, string text, CancellationToken cancellationToken)
    {
        if (userId != _adminTelegramId)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.", cancellationToken: cancellationToken);
            return;
        }

        var parts = text.Split();
        if (parts.Length < 2 || !long.TryParse(parts[1], out var targetUserId))
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Неверный формат. Используйте: /admin_remove_user <telegram_id>", cancellationToken: cancellationToken);
            return;
        }

        var user = await _dbService.GetUserAsync(targetUserId);
        if (user == null)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Пользователь не найден.", cancellationToken: cancellationToken);
            return;
        }

        var key = await _dbService.GetUserKeyAsync(targetUserId);
        if (key is not null)
        {
            await _outlineService.DeleteKeyAsync(key.KeyId);
        }
        await _dbService.RemoveUserFromWhitelistAsync(targetUserId);
        var message = $"✅ Пользователь `{user.Username}` (ID: `{user.TelegramId}`) удален из списка разрешенных.";
        await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
        _logger.Information("Admin {AdminId} removed user {UserId} from whitelist", userId, targetUserId);
    }

    private async Task HandleAdminSetLimitAsync(ITelegramBotClient botClient, long chatId, long userId, string text, CancellationToken cancellationToken)
    {
        if (userId != _adminTelegramId)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.", cancellationToken: cancellationToken);
            return;
        }

        var parts = text.Split();
        if (parts.Length < 3 || !long.TryParse(parts[1], out var targetUserId) || !int.TryParse(parts[2], out var limitGb))
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Неверный формат. Используйте: /admin_set_limit <telegram_id> <limit_gb>", cancellationToken: cancellationToken);
            return;
        }

        var key = await _dbService.GetUserKeyAsync(targetUserId);
        if (key == null)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ У пользователя нет ключа.", cancellationToken: cancellationToken);
            return;
        }

        await _dbService.UpdateKeyDataLimitAsync(targetUserId, limitGb);
        var message = $"✅ Лимит пользователя `{key.KeyName}` (ID: `{targetUserId}`) обновлен на `{limitGb} ГБ`.";
        await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
        _logger.Information("Admin {AdminId} set limit {LimitGb}GB for user {UserId}", userId, limitGb, targetUserId);
    }

    private async Task HandleAdminPendingUsersAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        if (userId != _adminTelegramId)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.", cancellationToken: cancellationToken);
            return;
        }

        var pendingUsers = await _dbService.GetPendingUsersAsync();
        if (pendingUsers.Count == 0)
        {
            await botClient.SendTextMessageAsync(chatId, "✅ Нет пользователей на одобрение.", cancellationToken: cancellationToken);
            return;
        }

        var message = "👥 Пользователи на одобрение\n\n";
        foreach (var user in pendingUsers)
        {
            message += $"ID: `{user.TelegramId}`\n";
            message += $"Username: `{user.Username}`\n";
            message += $"Зарегистрирован: {user.CreatedAt:yyyy-MM-dd HH:mm:ss}\n";
            message += "---\n";
        }

        await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
    }

    private async Task HandleAdminAllKeysAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        if (userId != _adminTelegramId)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.", cancellationToken: cancellationToken);
            return;
        }

        var allKeys = await _dbService.GetAllKeysAsync();
        if (allKeys.Count == 0)
        {
            await botClient.SendTextMessageAsync(chatId, "📭 Ключей еще не создано.", cancellationToken: cancellationToken);
            return;
        }

        var message = "🔑 Все ключи\n\n";
        foreach (var key in allKeys)
        {
            var user = await _dbService.GetUserAsync(key.TelegramId);
            var status = user?.IsWhitelisted ?? false ? "✅ Активен" : "❌ Неактивен";
            message += $"Пользователь: `{user?.Username ?? "Unknown"}`\n";
            message += $"Пользователь TG ID: `{user?.TelegramId.ToString() ?? "Unknown"}`\n";
            message += $"Ключ: {key.KeyName}\n";
            message += $"ID ключа: {key.KeyId}\n";
            message += $"Статус: {status}\n";
            message += $"Лимит: {key.DataLimitGb} ГБ\n";
            message += $"Обновлен: {key.LastUpdated:yyyy-MM-dd HH:mm:ss}\n";
            message += "---\n";
        }

        await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
    }

    private ReplyKeyboardMarkup GetMenuKeyboard(bool isAdmin)
    {
        var rows = new List<KeyboardButton[]>
        {
            new[]
            {
                new KeyboardButton("🔑 Мой ключ"),
                new KeyboardButton("➕ Создать ключ")
            },
            new[]
            {
                new KeyboardButton("❓ Помощь")
            }
        };

        if (isAdmin)
        {
            rows.Add(new[]
            {
                new KeyboardButton("👥 На одобрение"),
                new KeyboardButton("🗝 Все ключи")
            });
        }

        return new ReplyKeyboardMarkup(rows)
        {
            ResizeKeyboard = true,
            IsPersistent = true
        };
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.Error(exception, "Telegram bot polling error");
        return Task.CompletedTask;
    }
}
