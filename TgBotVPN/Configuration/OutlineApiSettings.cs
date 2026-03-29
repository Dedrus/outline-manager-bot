namespace TgBotVPN.Configuration;

public class OutlineApiSettings
{
    public const string SectionName = "OutlineApi";

    public string Url { get; set; } = null!;
    public string CertSha256 { get; set; } = null!;
}