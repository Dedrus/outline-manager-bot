namespace TgBotVPN.Configuration;

public class TelegramBotSettings
{
    public const string SectionName = "TelegramBot";
    
    public string Token { get; set; } = null!;
    public long AdminTelegramId { get; set; }
}
