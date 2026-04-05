namespace Recrutement_api.Templates;

public static class EmailTemplates
{
    private static string BaseLayout(string content, string previewText = "") => $@"
<!DOCTYPE html>
<html lang=""fr"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
  <title>RecruitSaaS</title>
  <style>
    body {{ margin:0; padding:0; background:#f4f6f9; font-family:'Segoe UI',Arial,sans-serif; }}
    .wrapper {{ max-width:600px; margin:40px auto; background:#fff; border-radius:12px; overflow:hidden; box-shadow:0 4px 24px rgba(0,0,0,0.08); }}
    .header {{ background:linear-gradient(135deg,#1e1b4b 0%,#312e81 100%); padding:32px 40px; text-align:center; }}
    .header h1 {{ color:#fff; margin:0; font-size:24px; font-weight:700; letter-spacing:1px; }}
    .header p {{ color:#a5b4fc; margin:6px 0 0; font-size:14px; }}
    .body {{ padding:40px; color:#374151; line-height:1.7; }}
    .body h2 {{ color:#1e1b4b; font-size:20px; margin-top:0; }}
    .body p {{ margin:0 0 16px; font-size:15px; }}
    .btn {{ display:inline-block; background:linear-gradient(135deg,#4f46e5,#7c3aed); color:#fff!important; text-decoration:none; padding:14px 32px; border-radius:8px; font-weight:600; font-size:15px; margin:16px 0; }}
    .info-box {{ background:#f0f4ff; border-left:4px solid #4f46e5; padding:16px 20px; border-radius:0 8px 8px 0; margin:20px 0; }}
    .info-box p {{ margin:4px 0; font-size:14px; color:#4b5563; }}
    .info-box strong {{ color:#1e1b4b; }}
    .divider {{ border:none; border-top:1px solid #e5e7eb; margin:24px 0; }}
    .footer {{ background:#f9fafb; padding:24px 40px; text-align:center; border-top:1px solid #e5e7eb; }}
    .footer p {{ margin:4px 0; font-size:12px; color:#9ca3af; }}
    .expire-warning {{ background:#fff7ed; border:1px solid #fed7aa; padding:12px 16px; border-radius:8px; font-size:13px; color:#92400e; margin-top:16px; }}
  </style>
</head>
<body>
  <div class=""wrapper"">
    <div class=""header"">
      <h1> RecruitSaaS</h1>
      <p>Plateforme de recrutement intelligente</p>
    </div>
    <div class=""body"">
      {content}
    </div>
    <div class=""footer"">
      <p>© {DateTime.UtcNow.Year} RecruitSaaS. Tous droits réservés.</p>
      <p>Cet email a été envoyé automatiquement, merci de ne pas y répondre.</p>
    </div>
  </div>
</body>
</html>";

    public static string ExpertInvitation(
        string toName,
        string companyName,
        string invitedByName,
        string invitationUrl,
        DateTime expiresAt,
        string tempPassword,
        string toEmail)
    {
        var content = $@"
<h2>Vous avez été invité(e) à rejoindre {companyName} </h2>
<p>Bonjour <strong>{toName}</strong>,</p>
<p>
  <strong>{invitedByName}</strong> vous invite à rejoindre l'équipe RH de 
  <strong>{companyName}</strong> sur RecruitSaaS en tant qu'expert.
</p>
<p>En tant qu'expert, vous pourrez :</p>
<ul style=""color:#4b5563;font-size:14px;line-height:2"">
  <li>Évaluer les candidatures</li>
  <li>Conduire des entretiens assistés par IA</li>
  <li>Collaborer avec votre équipe RH</li>
</ul>

<div class=""info-box"">
  <p><strong>Entreprise :</strong> {companyName}</p>
  <p><strong>Invité par :</strong> {invitedByName}</p>
  <p><strong> Email de connexion :</strong> {toEmail}</p>
  <p><strong> Mot de passe temporaire :</strong> 
    <code style=""background:#e0e7ff;padding:4px 10px;border-radius:4px;font-size:16px;letter-spacing:2px;font-weight:bold"">{tempPassword}</code>
  </p>
  <p><strong>Expire le :</strong> {expiresAt:dd/MM/yyyy à HH:mm} (UTC)</p>
</div>

<p style=""text-align:center"">
  <a href=""{invitationUrl}"" class=""btn""> Accepter l'invitation</a>
</p>

<div class=""expire-warning"">
   Ce lien d'invitation expire dans 48 heures. Après cette date, vous devrez demander une nouvelle invitation.
</div>

<hr class=""divider"" />
<p style=""font-size:13px;color:#6b7280"">
  Si vous ne pouvez pas cliquer sur le bouton, copiez ce lien dans votre navigateur :<br/>
  <span style=""color:#4f46e5;word-break:break-all"">{invitationUrl}</span>
</p>";

        return BaseLayout(content);
    }

    public static string Welcome(string toName, string loginUrl)
    {
        var content = $@"
<h2>Bienvenue sur RecruitSaaS ! </h2>
<p>Bonjour <strong>{toName}</strong>,</p>
<p>Votre compte a été créé avec succès. Vous pouvez maintenant vous connecter et commencer à utiliser la plateforme.</p>

<p style=""text-align:center"">
  <a href=""{loginUrl}"" class=""btn""> Se connecter</a>
</p>

<hr class=""divider"" />
<p style=""font-size:13px;color:#6b7280"">
  Si vous avez des questions, contactez notre support.
</p>";

        return BaseLayout(content);
    }

    public static string PasswordReset(string toName, string resetUrl, DateTime expiresAt)
    {
        var content = $@"
<h2>Réinitialisation de mot de passe </h2>
<p>Bonjour <strong>{toName}</strong>,</p>
<p>Vous avez demandé la réinitialisation de votre mot de passe. Cliquez sur le bouton ci-dessous pour en définir un nouveau.</p>

<p style=""text-align:center"">
  <a href=""{resetUrl}"" class=""btn""> Réinitialiser mon mot de passe</a>
</p>

<div class=""expire-warning"">
   Ce lien expire le {expiresAt:dd/MM/yyyy à HH:mm} (UTC).
</div>

<hr class=""divider"" />
<p style=""font-size:13px;color:#6b7280"">
  Si vous n'avez pas demandé cette réinitialisation, ignorez cet email. Votre mot de passe ne sera pas modifié.
</p>";

        return BaseLayout(content);
    }
}