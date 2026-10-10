using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Doorbell.Api.Authentication;

public sealed class DevelopmentTokenIssuer
{
    private const string Issuer = "doorbell-development";

    private readonly AuthenticationSettings _settings;

    public DevelopmentTokenIssuer(IOptions<AuthenticationSettings> settings)
    {
        _settings = settings.Value;
    }

    public (string Token, DateTime ExpiresAtUtc) Issue(
        string subject,
        string? name)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(Math.Max(1, _settings.Development.TokenLifetimeMinutes));

        var signingKey = string.IsNullOrWhiteSpace(_settings.Jwt.IssuerSigningKey)
            ? _settings.Development.SigningKey
            : _settings.Jwt.IssuerSigningKey!;

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", subject),
            new(ClaimTypes.NameIdentifier, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(name))
        {
            claims.Add(new Claim("name", name));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: string.IsNullOrWhiteSpace(_settings.Jwt.Audience)
                ? null
                : _settings.Jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
