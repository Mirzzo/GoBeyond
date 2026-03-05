using GoBeyond.Core.DTOs.Auth;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Interfaces;
using GoBeyond.Infrastructure.Utilities;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services;

public class AuthService(
    GoBeyondDbContext dbContext,
    IJwtTokenGenerator jwtTokenGenerator,
    IPasswordHasherService passwordHasherService) : IAuthService
{
    public async Task<AuthResponseDto> RegisterClientAsync(RegisterClientRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureEmailAvailableAsync(request.Email, cancellationToken);

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHasherService.Hash(request.Password),
            Role = UserRole.Client,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ClientProfile = new ClientProfile
            {
                Weight = request.Weight,
                Height = request.Height,
                Age = request.Age,
                FitnessLevel = request.FitnessLevel
            }
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponseDto> RegisterMentorAsync(RegisterMentorRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureEmailAvailableAsync(request.Email, cancellationToken);

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHasherService.Hash(request.Password),
            Role = UserRole.Mentor,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            MentorProfile = new MentorProfile
            {
                Bio = request.Bio,
                Age = request.Age,
                Category = request.Category,
                Price = request.Price,
                Status = MentorApprovalStatus.Pending,
                Certificates =
                [
                    new MentorCertificate
                    {
                        FileName = request.CertificateFileName,
                        FileUrl = request.CertificateFileUrl
                    }
                ]
            }
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .Include(x => x.MentorProfile)
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken)
            ?? throw new InvalidOperationException("Invalid credentials.");

        if (!user.IsActive)
        {
            throw new InvalidOperationException("User is blocked.");
        }

        if (!passwordHasherService.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid credentials.");
        }

        if (user.Role == UserRole.Mentor && user.MentorProfile?.Status != MentorApprovalStatus.Approved)
        {
            throw new InvalidOperationException("Mentor account is waiting for admin approval.");
        }

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenEntity = await dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == refreshToken, cancellationToken)
            ?? throw new InvalidOperationException("Invalid refresh token.");

        if (tokenEntity.IsRevoked || tokenEntity.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Refresh token expired or revoked.");
        }

        tokenEntity.IsRevoked = true;

        var response = await BuildAuthResponseAsync(tokenEntity.User, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (!passwordHasherService.Verify(currentPassword, user.PasswordHash))
        {
            throw new InvalidOperationException("Current password is invalid.");
        }

        user.PasswordHash = passwordHasherService.Hash(newPassword);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponseDto> BuildAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = jwtTokenGenerator.GenerateAccessToken(user);
        var refreshTokenValue = jwtTokenGenerator.GenerateRefreshToken();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto(
            accessToken,
            refreshTokenValue,
            new AuthUserDto(user.Id, $"{user.FirstName} {user.LastName}".Trim(), user.Email, user.Role));
    }

    private async Task EnsureEmailAvailableAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Email is already registered.");
        }
    }
}
