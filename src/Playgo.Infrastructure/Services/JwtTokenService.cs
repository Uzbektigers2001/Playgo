using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Playgo.Application.Common.Interfaces;
using Playgo.Domain.Entities;

namespace Playgo.Infrastructure.Services;

public class JwtTokenService : ITokenService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;
    private readonly int _refreshTokenDays;

    public JwtTokenService(IConfiguration configuration)
    {
        var section = configuration.GetSection("JwtSettings");
        _secret = section["Secret"] ?? throw new InvalidOperationException("JwtSettings:Secret is not configured.");
        _issuer = section["Issuer"] ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");
        _audience = section["Audience"] ?? throw new InvalidOperationException("JwtSettings:Audience is not configured.");
        _accessTokenMinutes = int.TryParse(section["AccessTokenMinutes"], out var atm) ? atm : 15;
        _refreshTokenDays = int.TryParse(section["RefreshTokenDays"], out var rtd) ? rtd : 30;
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("username", user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: GetAccessTokenExpiry(),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncoder.Encode(bytes);
    }

    public DateTime GetAccessTokenExpiry() => DateTime.UtcNow.AddMinutes(_accessTokenMinutes);

    public DateTime GetRefreshTokenExpiry() => DateTime.UtcNow.AddDays(_refreshTokenDays);
}
