using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBotVPN.Services;

public class AdminService
{
    private readonly AdminValidationService _adminValidationService;
    private readonly DatabaseService _dbService;
    private readonly OutlineApiService _outlineService;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        AdminValidationService adminValidationService,
        DatabaseService dbService,
        OutlineApiService outlineService,
        ITelegramBotClient botClient,
        ILogger<AdminService> logger)
    {
        _adminValidationService = adminValidationService;
        _dbService = dbService;
        _outlineService = outlineService;
        _botClient = botClient;
        _logger = logger;
    }

    public async Task HandleAdminAddUserAsync(long chatId, long userId, string text,
        CancellationToken cancellationToken)
    {
        if (!_adminValidationService.IsAdmin(userId))
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.",
                cancellationToken: cancellationToken);
            return;
        }

        var parts = text.Split();
        if (parts.Length < 2 || !long.TryParse(parts[1], out var targetUserId))
        {
            await _botClient.SendTextMessageAsync(chatId,
                "❌ Неверный формат. Используйте: /admin_add_user <telegram_id>", cancellationToken: cancellationToken);
            return;
        }

        var user = await _dbService.GetUserAsync(targetUserId);
        if (user == null)
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Пользователь не найден.",
                cancellationToken: cancellationToken);
            return;
        }

        await _dbService.AddUserToWhitelistAsync(targetUserId);
        var message = $"✅ Пользователь `{user.Username}` (ID: `{user.TelegramId}`) добавлен в список разрешенных.";
        await _botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);

        try
        {
            var userNotification = "🎉 Поздравляем! Администратор одобрил вашу заявку.\n\n" +
                                   "Теперь вы можете получить ваш VPN ключ, используя команду /my_key или кнопку \"🔑 Мой ключ\" в меню.";
            await _botClient.SendTextMessageAsync(targetUserId, userNotification, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception during user notification");
        }

        _logger.LogInformation("Admin {AdminId} added user {UserId} to whitelist", userId, targetUserId);
    }

    public async Task HandleAdminRemoveUserAsync(long chatId, long userId, string text,
        CancellationToken cancellationToken)
    {
        if (!_adminValidationService.IsAdmin(userId))
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.",
                cancellationToken: cancellationToken);
            return;
        }

        var parts = text.Split();
        if (parts.Length < 2 || !long.TryParse(parts[1], out var targetUserId))
        {
            await _botClient.SendTextMessageAsync(chatId,
                "❌ Неверный формат. Используйте: /admin_remove_user <telegram_id>",
                cancellationToken: cancellationToken);
            return;
        }

        var user = await _dbService.GetUserAsync(targetUserId);
        if (user == null)
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Пользователь не найден.",
                cancellationToken: cancellationToken);
            return;
        }

        var key = await _dbService.GetUserKeyAsync(targetUserId);
        bool keyDeleted = false;
        if (key is not null)
        {
            keyDeleted = await _outlineService.DeleteKeyAsync(key.KeyId);
        }

        if (keyDeleted)
        {
            await _dbService.RemoveUserFromWhitelistAsync(targetUserId);
            var message = $"✅ Пользователь `{user.Username}` (ID: `{user.TelegramId}`) удален из списка разрешенных.";
            await _botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
            _logger.LogInformation("Admin {AdminId} removed user {UserId} from whitelist", userId, targetUserId);
        }
        else
        {
            var message =
                $"❌ Пользователь `{user.Username}` (ID: `{user.TelegramId}`) не удалось удалить ключ доступа.";
            await _botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }
    }

    public async Task HandleAdminSetLimitAsync(long chatId, long userId, string text,
        CancellationToken cancellationToken)
    {
        if (!_adminValidationService.IsAdmin(userId))
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.",
                cancellationToken: cancellationToken);
            return;
        }

        var parts = text.Split();
        if (parts.Length < 3 || !long.TryParse(parts[1], out var targetUserId) ||
            !int.TryParse(parts[2], out var limitGb))
        {
            await _botClient.SendTextMessageAsync(chatId,
                "❌ Неверный формат. Используйте: /admin_set_limit <telegram_id> <limit_gb>",
                cancellationToken: cancellationToken);
            return;
        }

        var key = await _dbService.GetUserKeyAsync(targetUserId);
        if (key == null)
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ У пользователя нет ключа.",
                cancellationToken: cancellationToken);
            return;
        }

        // Update data limit on Outline API
        var success = await _outlineService.UpdateKeyDataLimitAsync(key.KeyId, limitGb);

        if (success)
        {
            // Update LastUpdated in database
            await _dbService.UpdateKeyDataLimitAsync(key.TelegramId, limitGb);
            var message = $"✅ Лимит пользователя `{key.KeyName}` (ID: `{targetUserId}`) обновлен на `{limitGb} ГБ`.";
            await _botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
            _logger.LogInformation("Admin {AdminId} set limit {LimitGb}GB for user {UserId}", userId, limitGb,
                targetUserId);
        }
        else
        {
            var message = $"❌ Не удалось обновить лимит трафика (ID: `{targetUserId}`)`.";
            await _botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
            _logger.LogWarning("Failed to update key {KeyName} (TelegramId: {TelegramId})",
                key.KeyName, key.TelegramId);
        }
    }

    public async Task HandleAdminPendingUsersAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        if (!_adminValidationService.IsAdmin(userId))
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.",
                cancellationToken: cancellationToken);
            return;
        }

        var pendingUsers = await _dbService.GetPendingUsersAsync();
        if (pendingUsers.Count == 0)
        {
            await _botClient.SendTextMessageAsync(chatId, "✅ Нет пользователей на одобрение.",
                cancellationToken: cancellationToken);
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

        await _botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }

    public async Task HandleAdminAllKeysAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        if (!_adminValidationService.IsAdmin(userId))
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.",
                cancellationToken: cancellationToken);
            return;
        }

        var allKeys = await _dbService.GetAllKeysWithUsersAsync();
        if (allKeys.Count == 0)
        {
            await _botClient.SendTextMessageAsync(chatId, "📭 Ключей еще не создано.",
                cancellationToken: cancellationToken);
            return;
        }

        var builder = new StringBuilder();
        var chunks = allKeys.Chunk(5);
        foreach (var chunk in chunks)
        {
            foreach (var keyWithUser in chunk)
            {
                var status = keyWithUser.TelegramUser.IsWhitelisted ? "✅ Активен" : "❌ Неактивен";

                // Экранируем специальные символы Markdown
                var username = EscapeMarkdown(keyWithUser.TelegramUser.Username);
                var keyName = EscapeMarkdown(keyWithUser.KeyName);
                var keyId = EscapeMarkdown(keyWithUser.KeyId);
                var lastUpdated = keyWithUser.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss");

                builder.Append($"Пользователь: {username}\n");
                builder.Append($"Пользователь TG ID: `{keyWithUser.TelegramUser.TelegramId}`\n");
                builder.Append($"Ключ: {keyName}\n");
                builder.Append($"ID ключа: {keyId}\n");
                builder.Append($"Статус: {status}\n");
                builder.Append($"Лимит: {keyWithUser.DataLimitGb} ГБ\n");
                builder.Append($"Обновлен: {lastUpdated}\n");
                builder.Append("---\n");
            }

            var messageText = builder.ToString();
            await _botClient.SendTextMessageAsync(chatId, messageText, parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
            builder.Clear();
            await Task.Delay(50, cancellationToken);
        }
    }

    public async Task HandleAdminBroadcastAsync(long chatId, long userId, string text,
        CancellationToken cancellationToken)
    {
        if (!_adminValidationService.IsAdmin(userId))
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Доступ запрещен. Только администратор.",
                cancellationToken: cancellationToken);
            return;
        }

        // Extract message content (everything after "/admin_broadcast ")
        var messageParts = text.Split(new[] { ' ' }, 2);
        if (messageParts.Length < 2 || string.IsNullOrWhiteSpace(messageParts[1]))
        {
            await _botClient.SendTextMessageAsync(chatId,
                "❌ Неверный формат. Используйте: /admin_broadcast <сообщение>", cancellationToken: cancellationToken);
            return;
        }

        var broadcastMessage = messageParts[1];
        await _botClient.SendTextMessageAsync(chatId, "⏳ Отправляю сообщение всем пользователям...",
            cancellationToken: cancellationToken);

        try
        {
            await SendBroadcastAsync(broadcastMessage, cancellationToken);
            await _botClient.SendTextMessageAsync(chatId, "✅ Сообщение успешно отправлено всем пользователям!",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send broadcast message");
            await _botClient.SendTextMessageAsync(chatId,
                "❌ Ошибка при отправке сообщения. Пожалуйста, попробуйте позже.", cancellationToken: cancellationToken);
        }
    }

    public async Task HandleHelpAsync(long chatId,
        CancellationToken cancellationToken)
    {
        var helpMessage = "📚 Доступные команды\n\n" +
                          "/start - Зарегистрироваться в боте\n" +
                          "/my_key - Получить текущий ключ (или создать новый)\n" +
                          "/help - Показать эту справку\n";


        helpMessage += "\n👨‍💼 Админ-команды\n" +
                       "/admin_add_user <telegram_id> - Добавить пользователя\n" +
                       "/admin_remove_user <telegram_id> - Удалить пользователя\n" +
                       "/admin_set_limit <telegram_id> <limit_gb> - Установить лимит\n" +
                       "/admin_pending_users - Список на одобрение\n" +
                       "/admin_all_keys - Список всех ключей\n" +
                       "/admin_broadcast <сообщение> - Отправить сообщение всем пользователям";

        await _botClient.SendTextMessageAsync(chatId, helpMessage, replyMarkup: GetMenuKeyboard(),
            cancellationToken: cancellationToken);
    }

    private ReplyKeyboardMarkup GetMenuKeyboard()
    {
        var rows = new List<KeyboardButton[]>
        {
            new[]
            {
                new KeyboardButton("🔑 Мой ключ")
            },
            new[]
            {
                new KeyboardButton("❓ Помощь")
            }
        };

        rows.Add(new[]
        {
            new KeyboardButton("👥 На одобрение"),
            new KeyboardButton("🗝 Все ключи")
        });


        return new ReplyKeyboardMarkup(rows)
        {
            ResizeKeyboard = true,
            IsPersistent = true
        };
    }

    private async Task SendBroadcastAsync(string message, CancellationToken cancellationToken = default)
    {
        var users = await _dbService.GetAllWhiteListUsersAsync();
        if (users.Count == 0)
        {
            _logger.LogInformation("No users found for broadcast");
            return;
        }

        _logger.LogInformation("Sending broadcast to {Count} users", users.Count);

        var successCount = 0;
        var failCount = 0;

        foreach (var user in users)
        {
            try
            {
                await _botClient.SendTextMessageAsync(
                    chatId: user.TelegramId,
                    text: message,
                    cancellationToken: cancellationToken);
                successCount++;
                _logger.LogInformation("Broadcast message sent to user {UserId} ({Username})", user.TelegramId,
                    user.Username);
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.LogWarning(ex, "Failed to send broadcast message to user {UserId} ({Username})",
                    user.TelegramId, user.Username);
                // Continue with other users even if one fails
            }

            // Add small delay to avoid hitting rate limits
            await Task.Delay(50, cancellationToken);
        }

        _logger.LogInformation("Broadcast completed: {SuccessCount} successful, {FailCount} failed", successCount,
            failCount);
    }

    public async Task HandleStartAsync(long chatId, CancellationToken cancellationToken)
    {
        var message = "👋 Добро пожаловать в Outline VPN бота!\n\n" +
                      "Вы администратор.\n\n" +
                      "Используйте меню снизу или введите /help для просмотра доступных команд.";

        await _botClient.SendTextMessageAsync(chatId, message, replyMarkup: GetMenuKeyboard(),
            cancellationToken: cancellationToken);
    }

    // Метод для экранирования Markdown-символов
    private string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Экранируем специальные символы Markdown
        char[] specialChars =
            { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
        foreach (char c in specialChars)
        {
            text = text.Replace(c.ToString(), $"\\{c}");
        }

        return text;
    }
}