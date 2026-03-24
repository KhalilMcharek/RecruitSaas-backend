namespace Recrutement_api.DTOs.Email;

public class ExpertInvitationEmailDto
{
    public string ToEmail { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string InvitedByName { get; set; } = string.Empty;
    public string InvitationUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string TemporaryPassword { get; set; } = string.Empty;
}

public class WelcomeEmailDto
{
    public string ToEmail { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
}

public class PasswordResetEmailDto
{
    public string ToEmail { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string ResetUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
public class GenericEmailDto
{
    public string ToEmail { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
}

public class EmailQueueItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
    public EmailQueueStatus Status { get; set; } = EmailQueueStatus.Pending;
}

public enum EmailQueueStatus
{
    Pending,
    Processing,
    Sent,
    Failed
}