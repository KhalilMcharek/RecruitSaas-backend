public class SavedJobDto
{
    public Guid Id { get; set; }
    public Guid OffreId { get; set; }
    public string Titre { get; set; } = string.Empty;
    public string? NomEntreprise { get; set; }
    public string? Localisation { get; set; }
    public string? TypeContrat { get; set; }
    public bool Teletravail { get; set; }
    public string? LogoUrl { get; set; }
    public DateTime CreeLe { get; set; }
    public DateTime SavedAt { get; set; }
}

public class SaveJobRequest
{
    public Guid OffreId { get; set; }  // Guid, not int
}