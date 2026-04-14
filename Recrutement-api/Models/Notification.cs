namespace Recrutement_api.Models
{
    public class Notification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ExpertId { get; set; }
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public bool Read { get; set; } = false;
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
        public Guid? OffreId { get; set; }
        public Guid? CandidatId { get; set; }
    }
}