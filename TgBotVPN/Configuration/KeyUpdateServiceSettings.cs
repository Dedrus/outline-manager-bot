namespace TgBotVPN.Configuration;

public class KeyUpdateServiceSettings
{
    public const string SectionName = "KeyUpdateService";
    
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public int UpdateIntervalDays { get; set; } = 30;
}
