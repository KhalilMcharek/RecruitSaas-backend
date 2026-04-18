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
            dto.ToName, dto.CompanyName, dto.InvitedByName,
            dto.InvitationUrl, dto.ExpiresAt, dto.TemporaryPassword, dto.ToEmail);
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

    public async Task SendGenericAsync(GenericEmailDto dto)
    {
        await SendAsync(dto.ToEmail, dto.ToName, dto.Subject, dto.HtmlBody);
    }

    // ── Interview Invite ──────────────────────────────────────────────────────
    public async Task SendInterviewInviteAsync(
        string toEmail,
        string toName,
        string titreOffre,
        string nomEntreprise,
        string schedulingUrl,
        string? messagePersonnalise = null)
    {
        // Email simple texte → arrive dans Principal au lieu de Promotions
        var messageBlock = string.IsNullOrEmpty(messagePersonnalise)
            ? ""
            : $@"<p style=""margin:16px 0;padding:12px 16px;background:#f9f9f9;border-left:3px solid #333;font-size:14px;color:#333;"">
                    {messagePersonnalise}
               </p>";

        var html = $@"<!DOCTYPE html>
<html>
<head><meta charset=""UTF-8""/></head>
<body style=""font-family:Arial,sans-serif;font-size:15px;color:#222;max-width:600px;margin:0 auto;padding:24px;"">

  <p>Bonjour {toName},</p>

  <p>Suite à votre candidature pour le poste de <strong>{titreOffre}</strong> chez <strong>{nomEntreprise}</strong>, nous souhaitons vous inviter à un entretien.</p>

  {messageBlock}

  <p>Merci de choisir un créneau disponible en cliquant sur le lien ci-dessous :</p>

  <p><a href=""{schedulingUrl}"" style=""color:#1a6e4a;font-weight:bold;"">{schedulingUrl}</a></p>

  <p>Informations pratiques :</p>
  <ul>
    <li>Durée : 30 minutes</li>
    <li>Format : entretien virtuel</li>
    <li>Poste : {titreOffre}</li>
  </ul>

  <p>Ce lien est personnel, merci de ne pas le partager.</p>

  <p>Cordialement,<br/>
  L'équipe RH — {nomEntreprise}</p>

</body>
</html>";

        await SendAsync(
            toEmail, toName,
            $"Entretien — {titreOffre} ({nomEntreprise})",
            html);

        _logger.LogInformation(
            "[EMAIL] Interview invite sent to {Email} for {Poste}",
            toEmail, titreOffre);
    }

    // ── Core send ─────────────────────────────────────────────────────────────
    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        Console.WriteLine($"[DEBUG] SmtpHost='{_settings.SmtpHost}'");

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
}