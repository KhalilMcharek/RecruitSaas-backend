using Recrutement_api.Services.Interfaces;

namespace Recrutement_api.Services.Implementations
{
    public class LinkGeneratorService : ILinkGeneratorService
    {
        private readonly IConfiguration _configuration;

        public LinkGeneratorService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateOfferLink(Guid offerId)
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"];
            return $"{baseUrl}/offres/{offerId}";
        }
    }
}
