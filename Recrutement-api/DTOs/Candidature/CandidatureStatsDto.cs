namespace Recrutement_api.DTOs.Candidature
{
    public class CandidatureStatsDto
    {
        public int Total { get; set; }
        public int EnAttente { get; set; }
        public int Acceptees { get; set; }
        public int Refusees { get; set; }
        public float? ScoreMoyenIA { get; set; }
    }
}
