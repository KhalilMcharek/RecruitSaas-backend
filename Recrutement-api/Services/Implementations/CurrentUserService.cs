using Recrutement_api.Services.Interfaces;
using System.Security.Claims;

namespace Recrutement_api.Services.Implementations
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid UserId =>
            Guid.Parse(_httpContextAccessor
                .HttpContext
                .User
                .FindFirstValue("userId"));

        public string Role =>
            _httpContextAccessor
                .HttpContext
                .User
                .FindFirstValue("role");

        public Guid? TenantId =>
            GetGuidClaim("tenantId");

        public Guid? ExpertId =>
            GetGuidClaim("expertId");

        public Guid? CandidatId =>
            GetGuidClaim("candidatId");

        private Guid? GetGuidClaim(string claim)
        {
            var value = _httpContextAccessor
                .HttpContext
                .User
                .FindFirstValue(claim);

            if (string.IsNullOrEmpty(value))
                return null;

            return Guid.Parse(value);
        }
    }
}
