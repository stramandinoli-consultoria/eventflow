using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EventFlow.Application.Abstractions;
using EventFlow.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EventFlow.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "eventflow";
    public string Audience { get; set; } = "eventflow";
    /// <summary>Chave HMAC com pelo menos 32 caracteres. Em produção vem de variável de ambiente.</summary>
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

public static class AppClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string PartnerId = "partner_id";
}

/// <summary>Hash de senha com PBKDF2 (implementação do ASP.NET Core Identity).</summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<object> Inner = new();
    private static readonly object Subject = new();

    public string Hash(string password) => Inner.HashPassword(Subject, password);

    public bool Verify(string hash, string password) =>
        Inner.VerifyHashedPassword(Subject, hash, password) != PasswordVerificationResult.Failed;
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(AppClaims.Subject, user.Id),
            new(AppClaims.Email, user.Email),
            new(AppClaims.Name, user.Name),
            new(AppClaims.Role, System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(user.Role.ToString())),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        if (user.PartnerId is not null) claims.Add(new Claim(AppClaims.PartnerId, user.PartnerId));

        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(_options.SigningKey(), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

internal sealed class Crypto : ICrypto
{
    public string RandomToken(int bytes = 32) => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(bytes));

    public string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
