namespace Recrutement_api.DTOs.Candidature
{
    public class CandidatureResponseDto
    {
        public Guid Id { get; set; }

        // Candidat
        public string NomCandidat { get; set; }
        public string EmailCandidat { get; set; }

        /// <summary>Photo depuis CandidateProfiles.AvatarUrl (nullable).</summary>
        public string? AvatarUrl { get; set; }

        // Offre
        public Guid OffreId { get; set; }
        public string TitreOffre { get; set; }

        // Entreprise
        public Guid EntrepriseId { get; set; }
        public string NomEntreprise { get; set; }

        public string Statut { get; set; }
        public DateTime CreeLe { get; set; }
        public string CvUrl { get; set; }

        // AI Score (nullable - pas toutes les candidatures ont une analyse)
        public float? ScoreIA { get; set; }
    }
}
