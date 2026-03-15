using Microsoft.Extensions.Logging;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TgBotVPN.Services;

public class TelegramBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly UserService _userService;
    private readonly AdminService _adminService;
    private readonly DatabaseService _dbService;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly AdminValidationService _adminValidationService;

    public TelegramBotService(UserService userService,
        AdminService adminService,
        DatabaseService dbService,
        ITelegramBotClient botClient,
        ILogger<TelegramBotService> logger, 
        AdminValidationService adminValidationService)
    {
        _userService = userService;
        _adminService = adminService;
        _dbService = dbService;
        _botClient = botClient;
        _logger = logger;
        _adminValidationService = adminValidationService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message },
        };

        _logger.LogInformation("Telegram bot started");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token);

        var me = await _botClient.GetMeAsync(cancellationToken);
        _logger.LogInformation("Bot logged in as {BotUsername}", me.Username);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message?.Type != MessageType.Text)
            return;

        var message = update.Message;
        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? 0;
        if (userId == 0) return;

        var username = message.From?.Username ?? message.From?.FirstName ?? "Unknown";
        var text = message.Text ?? string.Empty;

        _logger.LogInformation("Received message from {UserId} ({Username}): {Text}", userId, username, text);

        try
        {
            // Register user if not exists
            var user = await _dbService.GetOrCreateUserAsync(userId, username);

            var isAdmin = user.IsAdmin;

            switch (text)
            {
                // Reply-menu buttons (they arrive as plain text messages)
                case "🔑 Мой ключ":
                    await _userService.HandleGetKeyAsync(chatId, userId, user.Username, cancellationToken);
                    break;
                case "❓ Помощь":
                    await HandleHelpAsync(chatId, isAdmin, cancellationToken);
                    break;

                case "👥 На одобрение":
                    await _adminService.HandleAdminPendingUsersAsync(chatId, userId, cancellationToken);
                    ;
                    break;
                case "🗝 Все ключи":
                    await _adminService.HandleAdminAllKeysAsync(chatId, userId, cancellationToken);
                    break;
                default:
                {
                    if (text.StartsWith("/start"))
                    {
                        await HandleStartAsync(chatId, userId, cancellationToken);
                    }
                    else if (text.StartsWith("/my_key"))
                    {
                        await _userService.HandleGetKeyAsync(chatId, userId, username, cancellationToken);
                    }
                    else if (text.StartsWith("/help"))
                    {
                        await HandleHelpAsync(chatId, isAdmin, cancellationToken);
                    }
                    else if (text.StartsWith("/admin_add_user"))
                    {
                        await _adminService.HandleAdminAddUserAsync(chatId, userId, text, cancellationToken);
                    }
                    else if (text.StartsWith("/admin_remove_user"))
                    {
                        await _adminService.HandleAdminRemoveUserAsync(chatId, userId, text, cancellationToken);
                    }
                    else if (text.StartsWith("/admin_set_limit"))
                    {
                        await _adminService.HandleAdminSetLimitAsync(chatId, userId, text, cancellationToken);
                    }
                    else if (text.StartsWith("/admin_pending_users"))
                    {
                        await _adminService.HandleAdminPendingUsersAsync(chatId, userId, cancellationToken);
                    }
                    else if (text.StartsWith("/admin_all_keys"))
                    {
                        await _adminService.HandleAdminAllKeysAsync(chatId, userId, cancellationToken);
                    }
                    else if (text.StartsWith("/admin_broadcast"))
                    {
                        await _adminService.HandleAdminBroadcastAsync(chatId, userId, text, cancellationToken);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            "❓ Неизвестная команда. Введите /help для списка доступных команд.",
                            cancellationToken: cancellationToken);
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from {UserId}", userId);
            await botClient.SendTextMessageAsync(chatId, "❌ Произошла ошибка. Пожалуйста, попробуйте позже.",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleStartAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        var isAdmin = _adminValidationService.IsAdmin(userId);
        if (!isAdmin)
        {
            await _userService.HandleStartAsync(chatId, cancellationToken);
        }
        else
        {
            await _adminService.HandleStartAsync(chatId, cancellationToken);
        }
    }

    private async Task HandleHelpAsync(long chatId, bool isAdmin,
        CancellationToken cancellationToken)
    {
        if (isAdmin)
        {
            await _adminService.HandleHelpAsync(chatId, cancellationToken);
        }
        else
        {
            await _userService.HandleHelpAsync(chatId, cancellationToken);
        }
    }


    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram bot polling error");
        return Task.CompletedTask;
    }
}