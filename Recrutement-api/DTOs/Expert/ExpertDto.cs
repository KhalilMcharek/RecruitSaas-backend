namespace Recrutement_api.DTOs.Expert
{
    public class ExpertDto
    {
        public Guid Id { get; set; }

   
        public Guid UtilisateurId { get; set; }
        public string Nom { get; set; }
        public Guid EntrepriseId { get; set; }
        public string? NomEntreprise { get; set; } 
        public string Poste { get; set; }
    }
     // ── Candidature vue par l'expert ─────────────────────────────────────────
    public class CandidatureExpertDto
    {
        public Guid Id { get; set; }
 
        // Offre
        public Guid OffreId { get; set; }
        public string OffreTitre { get; set; }
 
        // Candidat
        public Guid CandidatId { get; set; }
        public string CandidatNomComplet { get; set; }
        public string CandidatEmail { get; set; }
        public string CandidatTelephone { get; set; }
 
        // Candidature
        public string CvUrl { get; set; }
        public string Statut { get; set; }
        public DateTime CreeLe { get; set; }
 
        // Champs personnalisés remplis par le candidat (désérialisé depuis FormulaireResponses JSON)
        public List<FormulaireReponseDto> FormulaireResponses { get; set; } = new();
 
        // Avis de l'expert connecté (null si pas encore noté)
        public AvisExpertDetailDto AvisExpert { get; set; }
    }
 
    // ── Réponse d'un champ personnalisé ──────────────────────────────────────
    public class FormulaireReponseDto
    {
        public string Label { get; set; }
        public string Type { get; set; }    // text, select, checkbox, date…
        public string Valeur { get; set; }
    }
 
    // ── Avis expert (lecture) ─────────────────────────────────────────────────
    public class AvisExpertDetailDto
    {
        public Guid Id { get; set; }
        public float Score { get; set; }        // 0.0 – 5.0 (aligné sur le modèle)
        public string Commentaire { get; set; }
        public DateTime CreeLe { get; set; }
    }
 
    // ── Soumettre / modifier un avis ─────────────────────────────────────────
    public class SoumettreAvisDto
    {
        public Guid CandidatureId { get; set; }
        public float Score { get; set; }        // 0.0 – 5.0
        public string Commentaire { get; set; }
    }
 
    // ── Résumé offre pour la sidebar ──────────────────────────────────────────
    public class OffreAssigneeDto
    {
        public Guid OffreId { get; set; }
        public string Titre { get; set; }
        public int NombreCandidatures { get; set; }
    }}