using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Recrutement_api.Configuration;
using Recrutement_api.DTOs.Email;
using Recrutement_api.Templates;

namespace Recrutement_api.Services.Shared;

public class EmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendInvitationEmail(string email) =>
    await SendExpertInvitationAsync(new ExpertInvitationEmailDto
    {
        ToEmail = email,
        ToName = email,
        CompanyName = "votre entreprise",
        InvitedByName = "L'équipe RH",
        InvitationUrl = $"{_settings.FrontendBaseUrl}/login",
        ExpiresAt = DateTime.UtcNow.AddHours(48),
        TemporaryPassword = "changeme123"  
    });

    public async Task SendExpertInvitationAsync(ExpertInvitationEmailDto dto)
    {
    var html = EmailTemplates.ExpertInvitation(
        dto.ToName,
        dto.CompanyName,
        dto.InvitedByName,
        dto.InvitationUrl,
        dto.ExpiresAt,
        dto.TemporaryPassword,
        dto.ToEmail); 

    await SendAsync(dto.ToEmail, dto.ToName,
        $"Vos accès RecruitSaaS - {dto.CompanyName}", html);
    _logger.LogInformation("Invitation sent to {Email}", dto.ToEmail);
    }

    public async Task SendWelcomeEmailAsync(WelcomeEmailDto dto)
    {
        var html = EmailTemplates.Welcome(dto.ToName, dto.LoginUrl);
        await SendAsync(dto.ToEmail, dto.ToName, "Bienvenue sur RecruitSaaS !", html);
    }

    public async Task SendPasswordResetAsync(PasswordResetEmailDto dto)
    {
        var html = EmailTemplates.PasswordReset(dto.ToName, dto.ResetUrl, dto.ExpiresAt);
        await SendAsync(dto.ToEmail, dto.ToName, "Réinitialisation de mot de passe", html);
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        Console.WriteLine($"[DEBUG] SmtpHost='{_settings.SmtpHost}'");
        // En dev sans config SMTP : log uniquement
        if (string.IsNullOrEmpty(_settings.SmtpHost))
        {
            _logger.LogInformation("[EMAIL SIMULATION] To={Email} Subject={Subject}", toEmail, subject);
            Console.WriteLine($"[EMAIL] To: {toEmail} | Subject: {subject}");
            return;
        }

        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = true,  
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,  
            Credentials = new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword)
        };
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = subject,
            IsBodyHtml = true,
            Body = htmlBody
        };
        message.To.Add(new MailAddress(toEmail, toName));

        await client.SendMailAsync(message);
    }
    public async Task SendGenericAsync(GenericEmailDto dto)
    {
    await SendAsync(dto.ToEmail, dto.ToName, dto.Subject, dto.HtmlBody);
    }
}