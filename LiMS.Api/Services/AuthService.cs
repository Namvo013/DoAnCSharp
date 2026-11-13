using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LiMS.Api.Contracts;
using LiMS.Api.Data;
using LiMS.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LiMS.Api.Services;

public sealed class JwtOptions
{
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string SigningKey { get; set; }
    public int AccessTokenMinutes { get; set; } = 60;
}

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<bool> ChangePasswordAsync(int accountId, ChangePasswordRequest request, CancellationToken cancellationToken);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(Account account);
}

public sealed class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(Account account)
    {
        var jwtOptions = options.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtOptions.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.AccountId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, account.Username),
            new Claim(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
            new Claim(ClaimTypes.Name, account.Username),
            new Claim(ClaimTypes.Role, account.Role)
        };
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            jwtOptions.Issuer,
            jwtOptions.Audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

public sealed class AuthService(
    LiMSDbContext db,
    IPasswordHasher<Account> passwordHasher,
    ITokenService tokenService) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var account = await db.Accounts
            .Include(x => x.Reader)
            .SingleOrDefaultAsync(
                x => x.Username == username || x.Email == username,
                cancellationToken);

        if (account is null || !account.IsActive)
            return null;

        var verification = passwordHasher.VerifyHashedPassword(
            account,
            account.PasswordHash,
            request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return null;

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = passwordHasher.HashPassword(account, request.Password);
            await db.SaveChangesAsync(cancellationToken);
        }

        var token = tokenService.CreateToken(account);
        return new LoginResponse(
            account.AccountId,
            account.Reader?.CardNo ?? account.Username,
            account.FullName,
            account.Email,
            account.Role,
            token.Token,
            token.ExpiresAt);
    }

    public async Task<bool> ChangePasswordAsync(
        int accountId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(
            x => x.AccountId == accountId && x.IsActive,
            cancellationToken);
        if (account is null)
            return false;

        if (passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.CurrentPassword)
            == PasswordVerificationResult.Failed)
            return false;

        account.PasswordHash = passwordHasher.HashPassword(account, request.NewPassword);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
