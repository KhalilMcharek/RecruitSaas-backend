namespace Recrutement_api.DTOs
{
    public class OffreListDTO
    {
        public Guid Id { get; set; }
        public string Titre { get; set; }
        public string Localisation { get; set; }
        public DateTime CreeLe { get; set; }
    }

    public class OffreDetailDTO
    {
        public Guid Id { get; set; }
        public string Titre { get; set; }
        public string Description { get; set; }
        public string Localisation { get; set; }
        public DateTime CreeLe { get; set; }
    }
    public class PostulerDTO
{
    public Guid OffreId { get; set; }
    public Guid CandidatId { get; set; }
    public string CvUrl { get; set; }
}
}