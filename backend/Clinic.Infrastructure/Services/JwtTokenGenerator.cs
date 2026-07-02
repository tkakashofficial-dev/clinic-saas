using Clinic.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Clinic.Infrastructure.Services;

public class JwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public (string token, DateTime expiresAt) Generate(
        Guid systemUserId,
        Guid tenantUserId,
        Guid tenantId,
        string email,
        string fullName,
        IReadOnlyCollection<string> roles)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpiresInMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, systemUserId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, fullName),
            new("tenant_id", tenantId.ToString()),
            new("tenant_user_id", tenantUserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // One claim per role — [Authorize(Roles = "A,B")] matches if the
        // user holds ANY of them, so Admin+Doctor combos just work
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}