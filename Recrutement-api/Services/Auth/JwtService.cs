using Microsoft.IdentityModel.Tokens;
using Recrutement_api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
namespace Recrutement_api.Services
{
    public interface IJwtService
    {
        string GenerateToken(Utilisateur user, Guid? tenantId, Guid? expertId, Guid? candidatId);
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config)
        {
            _config = config;
        }
        public string GenerateToken(
        Utilisateur user,
        Guid? tenantId = null,
        Guid? expertId = null,
        Guid? candidatId = null)
        {
            var secret = _config["Jwt:Secret"]!;
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];

            var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim("userId", user.Id.ToString()),
        new Claim("role", user.Role.ToString())
    };

            if (tenantId.HasValue)
                claims.Add(new Claim("tenantId", tenantId.Value.ToString()));

            if (expertId.HasValue)
                claims.Add(new Claim("expertId", expertId.Value.ToString()));

            if (candidatId.HasValue)
                claims.Add(new Claim("candidatId", candidatId.Value.ToString()));

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secret));

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(3),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}