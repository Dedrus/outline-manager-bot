namespace TgBotVPN.Configuration;

public class KeyUpdateServiceSettings
{
    public const string SectionName = "KeyUpdateService";
    
    public int CheckIntervalSeconds { get; set; } = 10;
    public int UpdateIntervalDays { get; set; } = 30;
}
