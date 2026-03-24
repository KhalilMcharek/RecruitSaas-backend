namespace Recrutement_api.Configuration;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@recruitsaas.com";
    public string FromName { get; set; } = "RecruitSaaS";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}