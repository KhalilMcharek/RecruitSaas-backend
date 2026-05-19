namespace Recrutement_api.DTOs.Entretien
{
    public class PlanifierDto
    {
        public List<DateTime> Creneaux { get; set; } = new();
        public string? Message { get; set; }
        public string? QuestionsJson { get; set; }
        public string Langue { get; set; } = "FR";
    }

    public class ConfirmerCreneauDto
    {
        public DateTime DateChoisie { get; set; }
    }

    public class InterviewReplyDto
    {
        public string QuestionActuelle { get; set; } = string.Empty;
        public string ReponseCandidat { get; set; } = string.Empty;
        public string Langue { get; set; } = "FR";
    }

    public class TtsDto
    {
        public string Text { get; set; } = string.Empty;
        public string Langue { get; set; } = "FR";
    }

    public class SaveTranscriptDto
    {
        public string Transcript { get; set; } = string.Empty;
    }
}