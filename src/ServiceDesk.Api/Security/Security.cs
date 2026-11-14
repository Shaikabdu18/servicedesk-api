using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Security;

public static class AppClaims
{
    public const string UserId = JwtRegisteredClaimNames.Sub;
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "ServiceDesk";

    [Required]
    public string Audience { get; set; } = "ServiceDesk.Clients";

    /// <summary>HMAC-SHA256 key. Must be at least 32 characters; set it from a secret in production.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int ExpiryMinutes { get; set; } = 60;

    public SymmetricSecurityKey CreateKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(User user);
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaims.UserId, user.Id.ToString()),
                new Claim(AppClaims.Email, user.Email),
                new Claim(AppClaims.Name, user.FullName),
                new Claim(AppClaims.Role, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            SigningCredentials = new SigningCredentials(jwt.CreateKey(), SecurityAlgorithms.HmacSha256)
        };

        return (_handler.CreateToken(descriptor), expires);
    }
}

/// <summary>The authenticated caller, read from JWT claims.</summary>
public interface ICurrentUser
{
    int Id { get; }
    Role Role { get; }
    bool IsStaff { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No active HTTP request.");

    public int Id => int.TryParse(Principal.FindFirst(AppClaims.UserId)?.Value, out var id)
        ? id
        : throw new ForbiddenException("The access token has no user id.");

    public Role Role => Enum.TryParse<Role>(Principal.FindFirst(AppClaims.Role)?.Value, out var role)
        ? role
        : Role.Customer;

    public bool IsStaff => Role is Role.Agent or Role.Admin;
}
