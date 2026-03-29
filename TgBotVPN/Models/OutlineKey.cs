using System;

namespace TgBotVPN.Models;

public class OutlineKey
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string KeyId { get; set; } = null!;
    public string KeyName { get; set; } = null!;
    public string AccessUrl { get; set; } = null!;
    public int DataLimitGb { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime CreatedAt { get; set; }
    public TelegramUser TelegramUser { get; set; } = null!;
}