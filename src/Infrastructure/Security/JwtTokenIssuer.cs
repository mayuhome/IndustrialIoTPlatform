using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Abstractions;
using Application.Auth.Models;
using Domain;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

public sealed class JwtTokenIssuer(
    string issuer,
    string audience,
    string signingKey,
    int expiryMinutes) : ITokenIssuer
{
    private readonly string _issuer = issuer;
    private readonly string _audience = audience;
    private readonly string _signingKey = signingKey;
    private readonly int _expiryMinutes = expiryMinutes <= 0 ? 60 : expiryMinutes;

    public AuthTokenResult Issue(User user)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_expiryMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthTokenResult(accessToken, expiresAtUtc, user.Id, user.Username, user.Role.ToString());
    }
}
