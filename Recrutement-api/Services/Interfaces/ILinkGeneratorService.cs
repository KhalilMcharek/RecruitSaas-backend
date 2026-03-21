namespace Recrutement_api.Services.Interfaces
{
    public interface ILinkGeneratorService
    {
        public string GenerateOfferLink(Guid offerId);
    }
}
