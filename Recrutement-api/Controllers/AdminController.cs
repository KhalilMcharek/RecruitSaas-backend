using Microsoft.AspNetCore.Mvc;
using Recrutement_api.DTOs.TenantRequest;

namespace Recrutement_api.Controllers
{
    [Route("api/admin")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        // ─────────────────────────────────────────────────────────────
        // Données statiques en mémoire — pas de base de données,
        // pas de Model, pas de TenantRequest.cs
        // ─────────────────────────────────────────────────────────────
        private static readonly List<TenantRequestDetailDto> _requests = new()
        {
            new TenantRequestDetailDto
            {
                Id          = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                CompanyName = "NexGen Solutions",
                RNE         = "834-921-042",
                Owner       = "Marcus Wright",
                WorkEmail   = "m.wright@nexgen.io",
                Industry    = "nexgen-recruit.com",
                Status      = "Pending",
                CreatedAt   = new DateTime(2025, 1, 10, 9, 0, 0, DateTimeKind.Utc)
            },
            new TenantRequestDetailDto
            {
                Id          = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
                CompanyName = "Lumina Tech",
                RNE         = "512-883-294",
                Owner       = "Sarah Chen",
                WorkEmail   = "chen.s@lumina.tech",
                Industry    = "jobs.lumina.tech",
                Status      = "Pending",
                CreatedAt   = new DateTime(2025, 1, 12, 11, 30, 0, DateTimeKind.Utc)
            },
            new TenantRequestDetailDto
            {
                Id          = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
                CompanyName = "Blue Horizon Inc",
                RNE         = "209-445-121",
                Owner       = "John Doe",
                WorkEmail   = "j.doe@bluehorizon.com",
                Industry    = "careers.bluehorizon.com",
                Status      = "Pending",
                CreatedAt   = new DateTime(2025, 1, 8, 14, 0, 0, DateTimeKind.Utc)
            },
            new TenantRequestDetailDto
            {
                Id          = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004"),
                CompanyName = "Prime Logistics",
                RNE         = "902-113-562",
                Owner       = "Eleanor Rigby",
                WorkEmail   = "eleanor@primelog.net",
                Industry    = "primelog.net/jobs",
                Status      = "Pending",
                CreatedAt   = new DateTime(2025, 1, 5, 8, 45, 0, DateTimeKind.Utc)
            },
            new TenantRequestDetailDto
            {
                Id          = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005"),
                CompanyName = "Apex Talent Group",
                RNE         = "671-334-890",
                Owner       = "James Okafor",
                WorkEmail   = "j.okafor@apextalent.com",
                Industry    = "apextalent.com/jobs",
                Status      = "Pending",
                CreatedAt   = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc)
            },
            new TenantRequestDetailDto
            {
                Id          = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000006"),
                CompanyName = "Visionary HR",
                RNE         = "445-772-013",
                Owner       = "Amina Bouzid",
                WorkEmail   = "a.bouzid@visionaryhr.tn",
                Industry    = "visionaryhr.tn",
                Status      = "Pending",
                CreatedAt   = new DateTime(2025, 1, 17, 8, 30, 0, DateTimeKind.Utc)
            },
            new TenantRequestDetailDto
            {
                Id          = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000007"),
                CompanyName = "TalentBridge",
                RNE         = "339-601-228",
                Owner       = "Karim Mansouri",
                WorkEmail   = "k.mansouri@talentbridge.io",
                Industry    = "talentbridge.io",
                Status      = "Pending",
                CreatedAt   = new DateTime(2025, 1, 19, 14, 15, 0, DateTimeKind.Utc)
            },
            new TenantRequestDetailDto
            {
                Id          = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000008"),
                CompanyName = "Orion Staffing",
                RNE         = "784-220-556",
                Owner       = "Lena Fischer",
                WorkEmail   = "l.fischer@orionstaffing.de",
                Industry    = "orionstaffing.de/careers",
                Status      = "Pending",
                CreatedAt   = new DateTime(2025, 1, 21, 11, 0, 0, DateTimeKind.Utc)
            },
            new TenantRequestDetailDto
            {
                Id          = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009"),
                CompanyName = "SmartHire Pro",
                RNE         = "120-987-443",
                Owner       = "Youssef Ben Ali",
                WorkEmail   = "y.benali@smarthirepro.com",
                Industry    = "smarthirepro.com",
                Status      = "Pending",
                CreatedAt   = new DateTime(2025, 1, 23, 9, 45, 0, DateTimeKind.Utc)
            },
            new TenantRequestDetailDto
            {
                Id          = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000010"),
                CompanyName = "CloudWork RH",
                RNE         = "567-843-991",
                Owner       = "Sofia Martins",
                WorkEmail   = "s.martins@cloudworkrh.eu",
                Industry    = "cloudworkrh.eu",
                Status      = "Pending",
                CreatedAt   = new DateTime(2025, 1, 25, 16, 0, 0, DateTimeKind.Utc)
            }
        };

        // ============================================================
        // 1️⃣  LISTE DE TOUTES LES DEMANDES
        // GET: api/admin/tenant-requests
        // ============================================================
        [HttpGet("tenant-requests")]
        public IActionResult GetAllTenantRequests()
        {
            var list = _requests
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new TenantRequestListDto
                {
                    CompanyName = r.CompanyName,
                    RNE         = r.RNE,
                    Owner       = r.Owner,
                    Industry    = r.Industry,
                    Status      = r.Status
                })
                .ToList();

            return Ok(new { success = true, count = list.Count, data = list });
        }

        // ============================================================
        // 2️⃣  DÉTAIL D'UNE DEMANDE PAR NOM D'ENTREPRISE
        // GET: api/admin/tenant-requests/by-name/{companyName}
        // ============================================================
        [HttpGet("tenant-requests/by-name/{companyName}")]
        public IActionResult GetTenantRequestByName(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var request = _requests
                .FirstOrDefault(r => (r.CompanyName ?? "").ToLower() == decoded.ToLower());

            if (request == null)
                return NotFound(new
                {
                    success = false,
                    message = $"Aucune demande trouvée pour '{decoded}'"
                });

            return Ok(new { success = true, data = request });
        }

        // ============================================================
        // 3️⃣  APPROUVER UNE DEMANDE
        // PATCH: api/admin/tenant-requests/by-name/{companyName}/approve
        // ============================================================
        [HttpPatch("tenant-requests/by-name/{companyName}/approve")]
        public IActionResult ApproveTenantRequest(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var request = _requests
                .FirstOrDefault(r => (r.CompanyName ?? "").ToLower() == decoded.ToLower());

            if (request == null)
                return NotFound(new
                {
                    success = false,
                    message = $"Aucune demande trouvée pour '{decoded}'"
                });

            if (request.Status != "Pending")
                return BadRequest(new
                {
                    success = false,
                    message = $"Cette demande a déjà été traitée (statut actuel : {request.Status})"
                });

            request.Status = "Approved";

            return Ok(new
            {
                success = true,
                message = $"'{request.CompanyName}' a été approuvée",
                data    = new TenantStatusResponseDto
                {
                    CompanyName = request.CompanyName,
                    Status      = request.Status
                }
            });
        }

        // ============================================================
        // 4️⃣  REJETER UNE DEMANDE
        // PATCH: api/admin/tenant-requests/by-name/{companyName}/reject
        // ============================================================
        [HttpPatch("tenant-requests/by-name/{companyName}/reject")]
        public IActionResult RejectTenantRequest(string companyName)
        {
            var decoded = Uri.UnescapeDataString(companyName);

            var request = _requests
                .FirstOrDefault(r => (r.CompanyName ?? "").ToLower() == decoded.ToLower());

            if (request == null)
                return NotFound(new
                {
                    success = false,
                    message = $"Aucune demande trouvée pour '{decoded}'"
                });

            if (request.Status != "Pending")
                return BadRequest(new
                {
                    success = false,
                    message = $"Cette demande a déjà été traitée (statut actuel : {request.Status})"
                });

            request.Status = "Rejected";

            return Ok(new
            {
                success = true,
                message = $"'{request.CompanyName}' a été rejetée",
                data    = new TenantStatusResponseDto
                {
                    CompanyName = request.CompanyName,
                    Status      = request.Status
                }
            });
        }
    }
}