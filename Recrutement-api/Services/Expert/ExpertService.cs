using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Recrutement_api.Data;
using Recrutement_api.DTOs.Expert;
using Recrutement_api.Models;
using Recrutement_api.Services.Interfaces;

namespace Recrutement_api.Services.Implementations
{
    public class ExpertService : IExpertService
    {
        private readonly ApplicationDbContext _db;

        // Extensions reconnues comme images (affichage direct)
        private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
        {
            "png", "jpg", "jpeg", "gif", "webp", "bmp", "tiff", "tif", "svg", "ico", "avif", "heic", "heif"
        };

        // Champs "système" à exclure de l'affichage formulaire (déjà affichés dans la carte candidat)
        private static readonly HashSet<string> SystemKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "fullName", "email"
        };

        public ExpertService(ApplicationDbContext db)
        {
            _db = db;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Offres assignées à l'expert (pour la sidebar)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<List<OffreAssigneeDto>> GetOffresAssigneesAsync(Guid expertId)
        {
            var assignations = await _db.AssignationsExperts
                .Where(a => a.ExpertId == expertId)
                .Include(a => a.Offre)
                .ToListAsync();

            var offreIds = assignations.Select(a => a.OffreId).ToList();

            var nbParOffre = await _db.Candidatures
                .Where(c => offreIds.Contains(c.OffreId))
                .GroupBy(c => c.OffreId)
                .Select(g => new { OffreId = g.Key, Count = g.Count() })
                .ToListAsync();

            return assignations.Select(a => new OffreAssigneeDto
            {
                OffreId = a.OffreId,
                Titre = a.Offre?.Titre ?? "—",
                NombreCandidatures = nbParOffre
                    .FirstOrDefault(x => x.OffreId == a.OffreId)?.Count ?? 0
            }).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Liste des candidatures avec filtres optionnels
        // ─────────────────────────────────────────────────────────────────────
        public async Task<List<CandidatureExpertDto>> GetCandidaturesAsync(
            Guid expertId,
            Guid? filtreOffreId = null,
            string filtreStatut = null)
        {
            var offreIds = await _db.AssignationsExperts
                .Where(a => a.ExpertId == expertId)
                .Select(a => a.OffreId)
                .ToListAsync();

            if (filtreOffreId.HasValue)
                offreIds = offreIds.Where(id => id == filtreOffreId.Value).ToList();

            var query = _db.Candidatures
                .Where(c => offreIds.Contains(c.OffreId))
                .Include(c => c.Offre)
                .Include(c => c.Candidat)
                    .ThenInclude(ca => ca.Utilisateur)
                .Include(c => c.Avis)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtreStatut))
                query = query.Where(c => c.Statut == filtreStatut);

            var candidatures = await query
                .OrderByDescending(c => c.CreeLe)
                .ToListAsync();

            return candidatures.Select(c => MapToDto(c, expertId)).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Détail d'une candidature (avec vérification d'accès)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<CandidatureExpertDto> GetCandidatureDetailAsync(
            Guid expertId,
            Guid candidatureId)
        {
            var candidature = await _db.Candidatures
                .Include(c => c.Offre)
                .Include(c => c.Candidat)
                    .ThenInclude(ca => ca.Utilisateur)
                .Include(c => c.Avis)
                .FirstOrDefaultAsync(c => c.Id == candidatureId);

            if (candidature == null)
                throw new KeyNotFoundException("Candidature introuvable.");

            var estAssigne = await _db.AssignationsExperts
                .AnyAsync(a => a.ExpertId == expertId && a.OffreId == candidature.OffreId);

            if (!estAssigne)
                throw new UnauthorizedAccessException(
                    "Vous n'êtes pas autorisé à consulter cette candidature.");

            return MapToDto(candidature, expertId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Soumettre ou mettre à jour un avis (upsert)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<AvisExpertDetailDto> SoumettreAvisAsync(
            Guid expertId,
            SoumettreAvisDto dto)
        {
            var candidature = await _db.Candidatures
                .FirstOrDefaultAsync(c => c.Id == dto.CandidatureId);

            if (candidature == null)
                throw new KeyNotFoundException("Candidature introuvable.");

            var estAssigne = await _db.AssignationsExperts
                .AnyAsync(a => a.ExpertId == expertId && a.OffreId == candidature.OffreId);

            if (!estAssigne)
                throw new UnauthorizedAccessException(
                    "Vous n'êtes pas autorisé à noter cette candidature.");

            var existant = await _db.AvisExperts
                .FirstOrDefaultAsync(a =>
                    a.ExpertId == expertId && a.CandidatureId == dto.CandidatureId);

            if (existant != null)
            {
                existant.Score = dto.Score;
                existant.Commentaire = dto.Commentaire;
            }
            else
            {
                existant = new AvisExpert
                {
                    Id = Guid.NewGuid(),
                    ExpertId = expertId,
                    CandidatureId = dto.CandidatureId,
                    Score = dto.Score,
                    Commentaire = dto.Commentaire,
                    CreeLe = DateTime.UtcNow
                };
                _db.AvisExperts.Add(existant);
            }

            await _db.SaveChangesAsync();

            return new AvisExpertDetailDto
            {
                Id = existant.Id,
                Score = existant.Score,
                Commentaire = existant.Commentaire,
                CreeLe = existant.CreeLe
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers privés
        // ─────────────────────────────────────────────────────────────────────

        private CandidatureExpertDto MapToDto(Candidature c, Guid expertId)
        {
            var avisExpert = c.Avis?.FirstOrDefault(a => a.ExpertId == expertId);

            return new CandidatureExpertDto
            {
                Id = c.Id,
                OffreId = c.OffreId,
                OffreTitre = c.Offre?.Titre ?? "—",
                CandidatId = c.CandidatId,
                CandidatNomComplet = c.Candidat?.Utilisateur != null
                    ? $"{c.Candidat.Utilisateur.Prenom} {c.Candidat.Utilisateur.Nom}".Trim()
                    : "Candidat inconnu",
                CandidatEmail = c.Candidat?.Utilisateur?.Email ?? "",
                CandidatTelephone = c.Candidat?.Telephone ?? "",
                CvUrl = c.CvUrl,
                Statut = c.Statut,
                CreeLe = c.CreeLe,
                // ✅ Nouveau parseur : lit le dictionnaire plat stocké par CandidatController
                FormulaireResponses = ParseFormulaireResponses(c.FormulaireResponses),
                AvisExpert = avisExpert != null
                    ? new AvisExpertDetailDto
                    {
                        Id = avisExpert.Id,
                        Score = avisExpert.Score,
                        Commentaire = avisExpert.Commentaire,
                        CreeLe = avisExpert.CreeLe
                    }
                    : null
            };
        }

        /// <summary>
        /// Désérialise le champ JSON FormulaireResponses.
        ///
        /// CandidatController stocke UN DICTIONNAIRE PLAT :
        ///   { "portfolioUrl": "https://...", "motivation": "...", "Question label": "valeur", ... }
        ///
        /// Cette méthode :
        ///   1. Tente d'abord le format dictionnaire (nouveau format correct)
        ///   2. Tente ensuite le format tableau [{label,type,valeur}] (ancien format)
        ///   3. Détermine automatiquement le type (file / image / text) depuis la valeur
        ///   4. Exclut les clés système (fullName, email) déjà affichées dans la carte
        /// </summary>
        private static List<FormulaireReponseDto> ParseFormulaireResponses(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // ── Tentative 1 : dictionnaire plat (format courant) ──────────────
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);
                if (dict != null && dict.Count > 0)
                {
                    var result = new List<FormulaireReponseDto>();

                    foreach (var kv in dict)
                    {
                        // Exclure les champs système déjà affichés
                        if (SystemKeys.Contains(kv.Key)) continue;

                        var valeur = kv.Value.ValueKind == JsonValueKind.String
                            ? kv.Value.GetString() ?? ""
                            : kv.Value.ToString();

                        result.Add(new FormulaireReponseDto
                        {
                            Label  = FormatLabel(kv.Key),
                            Type   = DetectType(valeur),
                            Valeur = valeur
                        });
                    }

                    return result;
                }
            }
            catch { /* pas un dictionnaire → essayer le format tableau */ }

            // ── Tentative 2 : tableau [{label,type,valeur}] (ancien format) ──
            try
            {
                var list = JsonSerializer.Deserialize<List<FormulaireReponseDto>>(json, options);
                if (list != null)
                {
                    // Ré-appliquer l'exclusion des clés système et la détection de type
                    return list
                        .Where(r => !SystemKeys.Contains(r.Label ?? ""))
                        .Select(r =>
                        {
                            if (string.IsNullOrEmpty(r.Type))
                                r.Type = DetectType(r.Valeur ?? "");
                            return r;
                        })
                        .ToList();
                }
            }
            catch { /* format inconnu */ }

            return new();
        }

        /// <summary>
        /// Détecte le type d'un champ depuis sa valeur :
        ///   - URL d'image Cloudinary  → "image"
        ///   - URL Cloudinary (raw/pdf) → "file"
        ///   - Autre URL               → "url"
        ///   - Tout le reste           → "text"
        /// </summary>
        private static string DetectType(string valeur)
        {
            if (string.IsNullOrWhiteSpace(valeur)) return "text";

            if (valeur.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                valeur.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Extraire l'extension depuis l'URL (ignorer les query strings)
                var path = valeur.Split('?')[0];
                var dot  = path.LastIndexOf('.');
                var ext  = dot >= 0 ? path.Substring(dot + 1).ToLowerInvariant() : "";

                if (ImageExts.Contains(ext)) return "image";
                if (ext == "pdf")            return "file";

                // Cloudinary raw (pas d'extension claire) → "file"
                if (valeur.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
                    return "file";

                return "url";
            }

            return "text";
        }

        /// <summary>
        /// Transforme une clé camelCase ou snake_case en libellé lisible.
        /// Ex: "portfolioUrl" → "Portfolio Url" | "cover_letter" → "Cover Letter"
        /// </summary>
        private static string FormatLabel(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;

            // snake_case → espaces
            key = key.Replace('_', ' ');

            // camelCase → espaces avant les majuscules
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                if (i > 0 && char.IsUpper(key[i]) && !char.IsWhiteSpace(key[i - 1]))
                    sb.Append(' ');
                sb.Append(key[i]);
            }

            // Capitaliser la première lettre
            var s = sb.ToString().Trim();
            return s.Length == 0 ? s : char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}