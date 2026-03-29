using System;

namespace TgBotVPN.Models;

public class TelegramUser
{
    public long TelegramId { get; set; }
    public string Username { get; set; } = null!;
    public bool IsWhitelisted { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public OutlineKey? OutlineKey { get; set; }
}