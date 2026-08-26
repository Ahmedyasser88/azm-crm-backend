using AzmCrm.Application.Features.Identity.DTOs;
using AzmCrm.Application.Localization;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Identity;
using AzmCrm.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzmCrm.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ApplicationDbContext context,
    IJwtTokenGenerator jwtTokenGenerator,
    IOptions<JwtSettings> jwtSettings,
    ILocalizationService localization,
    ILogger<IdentityService> logger) : IIdentityService
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;

    public async Task<Result<Guid>> RegisterAsync(
        string username,
        string email,
        string mobileNumber,
        string password,
        CancellationToken ct = default)
    {
        var existingUser = await userManager.FindByNameAsync(username);
        if (existingUser != null)
        {
            return Result<Guid>.Failure(localization[LocalizationKeys.Identity.UsernameTaken]);
        }

        existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            return Result<Guid>.Failure(localization[LocalizationKeys.Identity.EmailAlreadyRegistered]);
        }

        var mobileExists = await context.Users.AnyAsync(u => u.MobileNumber == mobileNumber, ct);
        if (mobileExists)
        {
            return Result<Guid>.Failure(localization[LocalizationKeys.Identity.MobileNumberAlreadyRegistered]);
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = username,
            FullName = username,
            Email = email,
            MobileNumber = mobileNumber,
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return Result<Guid>.Failure(errors);
        }

        logger.LogInformation("User {Username} created successfully with ID: {UserId}", username, user.Id);
        return Result<Guid>.Success(user.Id);
    }

    public async Task<Result<AuthenticationResponse>> LoginAsync(
        string usernameOrEmail,
        string password,
        string ipAddress,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByNameAsync(usernameOrEmail)
                   ?? await userManager.FindByEmailAsync(usernameOrEmail);

        if (user == null)
        {
            return Result<AuthenticationResponse>.Failure(localization[LocalizationKeys.Identity.InvalidCredentials]);
        }

        if (!user.IsActive)
        {
            return Result<AuthenticationResponse>.Failure(localization[LocalizationKeys.Identity.AccountInactive]);
        }

        var isPasswordValid = await userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
        {
            return Result<AuthenticationResponse>.Failure(localization[LocalizationKeys.Identity.InvalidCredentials]);
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenGenerator.GenerateAccessToken(user, roles);
        var refreshToken = jwtTokenGenerator.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresOn = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress
        };

        // Add explicitly via the DbSet rather than only the in-memory `user.RefreshTokens`
        // navigation collection: RefreshToken.Id is client-generated (Guid.CreateVersion7()),
        // and an entity reached only through a tracked parent's navigation — never passed to
        // Add()/Attach() itself — gets its initial EntityState inferred from whether its key
        // already looks set. Since it does, EF marks it Modified instead of Added and emits an
        // UPDATE for a row that doesn't exist yet (0 rows affected -> DbUpdateConcurrencyException).
        context.RefreshTokens.Add(refreshTokenEntity);
        user.RefreshTokens.Add(refreshTokenEntity);
        user.LastLoginOn = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);

        var response = new AuthenticationResponse(
            user.Id,
            user.UserName!,
            user.Email!,
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(120),
            refreshTokenEntity.ExpiresOn
        );

        return Result<AuthenticationResponse>.Success(response);
    }

    public async Task<Result<AuthenticationResponse>> RefreshTokenAsync(
        string refreshToken,
        string ipAddress,
        CancellationToken ct = default)
    {
        var token = await context.Set<RefreshToken>()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);

        if (token == null)
        {
            return Result<AuthenticationResponse>.Failure(localization[LocalizationKeys.Identity.InvalidRefreshToken]);
        }

        if (!token.IsActive)
        {
            return Result<AuthenticationResponse>.Failure(localization[LocalizationKeys.Identity.RefreshTokenNotActive]);
        }

        var user = token.User;

        if (!user.IsActive)
        {
            return Result<AuthenticationResponse>.Failure(localization[LocalizationKeys.Identity.AccountInactive]);
        }

        var roles = await userManager.GetRolesAsync(user);
        var newAccessToken = jwtTokenGenerator.GenerateAccessToken(user, roles);
        var newRefreshToken = jwtTokenGenerator.GenerateRefreshToken();

        token.RevokedOn = DateTime.UtcNow;
        token.RevokedByIp = ipAddress;
        token.ReplacedByToken = newRefreshToken;

        var newRefreshTokenEntity = new RefreshToken
        {
            Token = newRefreshToken,
            UserId = user.Id,
            ExpiresOn = DateTime.UtcNow.AddDays(7), // _jwtSettings.RefreshTokenExpirationDays
            CreatedByIp = ipAddress
        };

        // Same explicit-Add reasoning as LoginAsync above.
        context.RefreshTokens.Add(newRefreshTokenEntity);
        user.RefreshTokens.Add(newRefreshTokenEntity);
        await context.SaveChangesAsync(ct);

        var response = new AuthenticationResponse(
            user.Id,
            user.UserName!,
            user.Email!,
            newAccessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(120), // _jwtSettings.AccessTokenExpirationMinutes
            newRefreshTokenEntity.ExpiresOn
        );

        return Result<AuthenticationResponse>.Success(response);
    }

    public async Task<Result> RevokeTokenAsync(
        string refreshToken,
        string ipAddress,
        CancellationToken ct = default)
    {
        var token = await context.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);

        if (token == null)
        {
            return Result.Failure(localization[LocalizationKeys.Identity.InvalidRefreshToken]);
        }

        if (!token.IsActive)
        {
            return Result.Failure(localization[LocalizationKeys.Identity.TokenRevoked]);
        }

        token.RevokedOn = DateTime.UtcNow;
        token.RevokedByIp = ipAddress;

        await context.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await userManager.FindByIdAsync(userId.ToString());
    }

    public async Task<ApplicationUser> GetOrCreateExternalUserAsync(
        Guid userId,
        string username,
        string? email,
        IEnumerable<string> roles,
        CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user != null)
        {
            return user;
        }

        var safeUsername = $"user_{userId:N}".Substring(0, 20);

        user = new ApplicationUser
        {
            Id = userId,
            UserName = safeUsername,
            FullName = username,
            Email = email ?? $"{safeUsername}@external.user",
            MobileNumber = "0000000000",
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user);

        if (!result.Succeeded)
        {
            logger.LogError("Failed to create external user {UserId}: {Errors}",
                userId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            throw new InvalidOperationException($"Failed to create external user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        if (roles.Any())
        {
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var createRoleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                    if (!createRoleResult.Succeeded)
                    {
                        logger.LogWarning("Failed to create role {Role}: {Errors}",
                            role,
                            string.Join(", ", createRoleResult.Errors.Select(e => e.Description)));
                    }
                }
            }

            var roleResult = await userManager.AddToRolesAsync(user, roles);
            if (!roleResult.Succeeded)
            {
                logger.LogWarning("Failed to add roles to external user {UserId}: {Errors}",
                    userId,
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }
        }
        logger.LogInformation("External user {Username} (Display: {DisplayName}) created successfully with ID: {UserId}",
            safeUsername, username, userId);

        return user;
    }
}
