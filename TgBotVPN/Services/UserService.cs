using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgBotVPN.Configuration;

namespace TgBotVPN.Services;

public class UserService
{
    private readonly DatabaseService _dbService;
    private readonly OutlineApiService _outlineService;
    private readonly int _defaultDataLimitGb;
    private readonly ILogger<UserService> _logger;
    private readonly ITelegramBotClient _botClient;

    public UserService(
        DatabaseService dbService,
        OutlineApiService outlineService,
        IOptions<TelegramBotSettings> options,
        ILogger<UserService> logger, ITelegramBotClient botClient)
    {
        _dbService = dbService;
        _outlineService = outlineService;
        _defaultDataLimitGb = options.Value.DefaultDataLimitGb;
        _logger = logger;
        _botClient = botClient;
    }

    public async Task HandleStartAsync(long chatId, CancellationToken cancellationToken)
    {
        var message = "👋 Добро пожаловать в Outline VPN бота!\n\n" +
                      "Вы зарегистрированы. Пожалуйста, ожидайте одобрения администратора.\n\n" +
                      "Используйте меню снизу или введите /help для просмотра доступных команд.\n\n" +
                      "Пожалуйста ознакомьтесь с правилами:\n\n" +
                      "1. Нельзя раздавать торренты с включенным VPN, наш сервер могут забанить.\n\n" +
                      "2. Пожалуйста, не занимайтесь экстремизмом, терроризмом и преступной деятельностью через этот VPN.\n\n" +
                      "3. Уважайте других пользователей нашего VPN, у нас один сервер на всех.\n\n";

        await _botClient.SendTextMessageAsync(chatId, message, replyMarkup: GetMenuKeyboard(),
            cancellationToken: cancellationToken);
    }


    public async Task HandleGetKeyAsync(long chatId, long userId, string username,
        CancellationToken cancellationToken)
    {
        var isWhitelisted = await _dbService.IsUserWhitelistedAsync(userId);
        if (!isWhitelisted)
        {
            await _botClient.SendTextMessageAsync(chatId, "❌ Вы не в списке разрешенных.",
                cancellationToken: cancellationToken);
            return;
        }

        var key = await _dbService.GetUserKeyAsync(userId);
        if (key != null)
        {
            // Показываем существующий ключ
            var keyMessage = $"🔑 Ваш ключ\n\n" +
                             $"Имя: {key.KeyName}\n" +
                             $"Лимит данных: {key.DataLimitGb} ГБ\n" +
                             $"Последнее обновление: {key.LastUpdated:yyyy-MM-dd HH:mm:ss} UTC\n\n" +
                             $"Ссылка доступа (нажми для копирования):\n`{key.AccessUrl}`";
            await _botClient.SendTextMessageAsync(chatId, keyMessage, parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
            return;
        }

        // Создаем новый ключ если его еще нет
        await _botClient.SendTextMessageAsync(chatId, "⏳ Создаю ваш ключ...", cancellationToken: cancellationToken);

        try
        {
            var accessKey = await _outlineService.CreateKeyAsync(username, _defaultDataLimitGb);
            key = await _dbService.CreateKeyAsync(userId, accessKey.Id, accessKey.Name, accessKey.AccessUrl,
                _defaultDataLimitGb);

            var keyMessage = $"✅ Ключ успешно создан!\n\n" +
                             $"Ваш лимит данных: {_defaultDataLimitGb} ГБ\n\n" +
                             $"Ссылка доступа (нажми для копирования):\n`{accessKey.AccessUrl}`";
            await _botClient.SendTextMessageAsync(chatId, keyMessage, parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
            _logger.LogInformation("Key created for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create key for user {UserId}", userId);
            await _botClient.SendTextMessageAsync(chatId,
                "❌ Не удалось создать ключ. Пожалуйста, попробуйте позже.",
                cancellationToken: cancellationToken);
        }
    }

    public async Task HandleHelpAsync(long chatId, CancellationToken cancellationToken)
    {
        var helpMessage = "📚 Доступные команды\n\n" +
                          "/start - Зарегистрироваться в боте\n" +
                          "/my_key - Получить текущий ключ (или создать новый)\n" +
                          "/help - Показать эту справку\n" +
                          "Пожалуйста ознакомьтесь с правилами:\n\n" +
                          "1. Нельзя раздавать торренты с включенным VPN, наш сервер могут забанить.\n\n" +
                          "2. Пожалуйста, не занимайтесь экстремизмом, терроризмом и преступной деятельностью через этот VPN.\n\n" +
                          "3. Уважайте других пользователей нашего VPN, у нас один сервер на всех.\n\n";


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


        return new ReplyKeyboardMarkup(rows)
        {
            ResizeKeyboard = true,
            IsPersistent = true
        };
    }
}