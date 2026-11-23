using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using GumpStudio.Application.Abstractions;
using GumpStudio.Application.Common;
using GumpStudio.Application.Options;
using GumpStudio.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GumpStudio.Infrastructure.Identity;

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public AccessToken CreateAccessToken(User user, Guid? artistId)
    {
        var o = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(o.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(),
            ["email"] = user.Email,
            ["name"] = user.Name,
            ["role"] = user.Role.ToString()
        };
        if (artistId.HasValue) claims["artist_id"] = artistId.Value.ToString();

        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey)), SecurityAlgorithms.HmacSha256)
        };
        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    public string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    /// <summary>Só o hash do refresh token é guardado no banco.</summary>
    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}

public sealed class GoogleIdTokenValidator(IOptions<GoogleOptions> options, ILogger<GoogleIdTokenValidator> logger) : IGoogleIdTokenValidator
{
    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ClientId))
        {
            logger.LogError("Google:ClientId está vazio na API. Configure em appsettings.Local.json e reinicie.");
            throw new UnauthorizedException("Login com Google não configurado na API (Google:ClientId vazio).");
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { options.Value.ClientId },
                    IssuedAtClockTolerance = TimeSpan.FromMinutes(5),
                    ExpirationTimeClockTolerance = TimeSpan.FromMinutes(5),
                });
            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning("Token do Google rejeitado: {Reason}", ex.Message);
            throw new UnauthorizedException("Token do Google inválido ou expirado.");
        }
    }
}
