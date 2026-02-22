using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs;
using Recrutement_api.Models;
using Recrutement_api.Services;

namespace Recrutement_api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _context;

        public AuthController(IAuthService authService, ApplicationDbContext context)
        {
            _authService = authService;
            _context = context;
        }

[HttpPost("register")]
public async Task<IActionResult> RegisterCandidate([FromBody] RegisterDto dto)
{
    // Vérifie la validité du ModelState
    if (!ModelState.IsValid)
    {
        var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

        return BadRequest(new { Message = "Validation échouée", Errors = errors });
    }

    // Vérifie si l'email existe déjà
    var existingUser = await _context.Utilisateurs
        .FirstOrDefaultAsync(u => u.Email == dto.Email);

    if (existingUser != null)
        return BadRequest(new { Message = "Un utilisateur avec cet email existe déjà." });

    // Création de l'utilisateur
    var candidatUser = new Utilisateur
    {
        Id = Guid.NewGuid(),
        Email = dto.Email,
        Nom = string.IsNullOrWhiteSpace(dto.Nom) ? "Nom non précisé" : dto.Nom,
        Prenom = string.IsNullOrWhiteSpace(dto.Prenom) ? "Non précisé" : dto.Prenom,
        MotDePasseHash = _authService.HashPassword(dto.MotDePasse),
        Role = RoleUtilisateur.Candidat,
        CreeLe = DateTime.UtcNow
    };

    await _context.Utilisateurs.AddAsync(candidatUser);
    await _context.SaveChangesAsync(); // ⚠ Important pour générer l'Id

    // Création de l'entrée dans la table Candidats
    var candidat = new Candidat
    {
        Id = Guid.NewGuid(),
        UtilisateurId = candidatUser.Id,
        CreeLe = DateTime.UtcNow,
        Telephone =  "Non précisé"
        // ⚠ Ajoute ici toutes les colonnes NOT NULL de Candidat avec valeurs par défaut si nécessaire
    };

    await _context.Candidats.AddAsync(candidat);
    await _context.SaveChangesAsync(); // ⚠ Important pour insérer dans Candidats

    return Ok(new { Message = "Candidat enregistré avec succès." });
}

        // =============================================
        // REGISTER TENANT + ENTREPRISE (status Pending)
        // =============================================
      [HttpPost("company/register-request")]
public async Task<IActionResult> RegisterCompany([FromBody] CompanyRegisterDto dto)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    // Vérifie si l'utilisateur existe déjà
    var existingUser = await _context.Utilisateurs
        .FirstOrDefaultAsync(u => u.Email == dto.WorkEmail);

    if (existingUser != null)
        return BadRequest("Un utilisateur avec cet email existe déjà.");

    // Création de l'utilisateur CEO
    var ceo = new Utilisateur
    {
        Id = Guid.NewGuid(),
        Email = dto.WorkEmail,
        Nom = dto.tenantName ?? "Nom non précisé",
        Prenom = dto.Prenom ?? "Prénom non précisé",
        Role = RoleUtilisateur.Tenant,
        MotDePasseHash = _authService.HashPassword(dto.Password), // ta fonction de hash
        CreeLe = DateTime.UtcNow
    };

    await _context.Utilisateurs.AddAsync(ceo);

    // Création du tenant
    var tenant = new Tenant
    {
        Id = Guid.NewGuid(),
        CreeLe = DateTime.UtcNow,
        UtilisateurId = ceo.Id,
        Nom = dto.tenantName ,
    };

    await _context.Tenants.AddAsync(tenant);

    // Création de l'entreprise associée
   var entreprise = new Entreprise
{
    Id = Guid.NewGuid(),
    CreeLe = DateTime.UtcNow,
    Nom = string.IsNullOrWhiteSpace(dto.CompanyName) ? "Nom de l'entreprise" : dto.CompanyName,
    Description = string.IsNullOrWhiteSpace(dto.Description) ? "Description non fournie" : dto.Description,
    RNE = string.IsNullOrWhiteSpace(dto.RNE) ? "Non précisé" : dto.RNE,
    Secteur = string.IsNullOrWhiteSpace(dto.Secteur) ? string.Empty : dto.Secteur,
    LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? string.Empty : dto.LogoUrl,
    TenantId = tenant.Id
};

// Ajout à EF Core
await _context.Entreprises.AddAsync(entreprise);

// Sauvegarde dans la base de données
await _context.SaveChangesAsync();

    return Ok(new { Message = "Demande d'inscription enregistrée avec succès." });
}
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginDto dto)
{
    // Vérifie que l'email et le mot de passe sont fournis
    if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.MotDePasse))
        return BadRequest("Email et mot de passe obligatoires.");

    // Récupère l'utilisateur depuis la base
    var utilisateur = await _context.Utilisateurs
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Email == dto.Email);

    // Si utilisateur non trouvé ou mot de passe incorrect
    if (utilisateur == null || !_authService.VerifyPassword(dto.MotDePasse, utilisateur.MotDePasseHash))
        return Unauthorized("Email ou mot de passe incorrect.");

    // Vérification selon le rôle
    if (utilisateur.Role == RoleUtilisateur.Tenant)
    {
        // Vérifie que le tenant existe et est approuvé
        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UtilisateurId == utilisateur.Id);

        if (tenant == null || tenant.Statut != TenantStatus.Approved)
            return Unauthorized("Désolé, votre demande n'a pas été acceptée.");
    }
    // Pour les candidats, aucune vérification supplémentaire nécessaire

    // Génère le token / réponse d'authentification
    var authResponse = _authService.GenerateAuthResponse(utilisateur);

    return Ok(authResponse);
}}}