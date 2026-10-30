using System.Security.Claims;
using System.Text;
using flash_cards_multiple_api.Data.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace flash_cards_multiple_api.Services;

public class JwtSettings
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Key { get; init; } = "";
    public int ExpiresMinutes { get; init; } = 60;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}

public class TokenService(JwtSettings settings)
{
    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(settings.ExpiresMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
            ]),
            SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256),
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
