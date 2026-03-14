namespace TgBotVPN.Configuration;

public class TelegramBotSettings
{
    public const string SectionName = "TelegramBot";
    
    public string Token { get; set; } = null!;
    public long[] AdminTelegramIds { get; set; } = null!;
    
    public int DefaultDataLimitGb { get; set; } = 100;
}
