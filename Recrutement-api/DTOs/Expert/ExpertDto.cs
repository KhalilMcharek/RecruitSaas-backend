namespace Recrutement_api.DTOs.Expert
{
    public class ExpertDto
    {
        public Guid    Id            { get; set; }
        public Guid    UtilisateurId { get; set; }
        public string  Nom           { get; set; }
        public Guid    EntrepriseId  { get; set; }
        public string? NomEntreprise { get; set; }
        public string  Poste         { get; set; }
    }

    public class CandidatureExpertDto
    {
        public Guid   Id       { get; set; }
        public Guid   OffreId  { get; set; }
        public string OffreTitre { get; set; }
        public Guid   CandidatId { get; set; }
        public string CandidatNomComplet { get; set; }
        public string CandidatEmail      { get; set; }
        public string CandidatTelephone  { get; set; }
        public string CvUrl   { get; set; }
        public string Statut  { get; set; }
        public DateTime CreeLe { get; set; }
        public List<FormulaireReponseDto> FormulaireResponses { get; set; } = new();

        // Avis de l'expert connecté uniquement
        public AvisExpertDetailDto? AvisExpert { get; set; }

        // ── NOUVEAU : avis de TOUS les experts assignés ──
        public List<AvisExpertDetailDto> TousLesAvis { get; set; } = new();

        // ── NOUVEAU : score IA depuis AnalyseCV ──
        public float? ScoreIA { get; set; }
        public string? ResumeIA { get; set; }
    }

    public class FormulaireReponseDto
    {
        public string Label  { get; set; }
        public string Type   { get; set; }
        public string Valeur { get; set; }
    }

    public class AvisExpertDetailDto
    {
        public Guid    Id          { get; set; }
        public float   Score       { get; set; }
        public string  Commentaire { get; set; }
        public DateTime CreeLe     { get; set; }

        // ── NOUVEAU : nom de l'expert auteur de l'avis ──
        public string? ExpertNom { get; set; }
    }

    public class SoumettreAvisDto
    {
        public Guid   CandidatureId { get; set; }
        public float  Score         { get; set; }
        public string Commentaire   { get; set; }
    }

    public class OffreAssigneeDto
    {
        public Guid   OffreId             { get; set; }
        public string Titre               { get; set; }
        public int    NombreCandidatures  { get; set; }
    }
}