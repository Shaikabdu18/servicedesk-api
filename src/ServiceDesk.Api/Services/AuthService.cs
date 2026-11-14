using Microsoft.AspNetCore.Identity;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Repositories;
using ServiceDesk.Api.Security;

namespace ServiceDesk.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public sealed class AuthService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    ITokenService tokens,
    TimeProvider clock) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);

        if (await users.EmailExistsAsync(email, ct))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        // Self-registration always creates a customer; staff accounts are seeded or created by admins.
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = Role.Customer,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        users.Add(user);
        await unitOfWork.SaveChangesAsync(ct);

        return Issue(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(NormalizeEmail(request.Email), ct)
                   ?? throw new AuthenticationFailedException();

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            throw new AuthenticationFailedException();
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return Issue(user);
    }

    private AuthResponse Issue(User user)
    {
        var (token, expiresAtUtc) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAtUtc, user.ToDto());
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
