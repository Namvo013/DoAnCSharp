using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LiMS.Tests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_signs_claims_for_configured_issuer_and_audience()
    {
        var options = new JwtOptions
        {
            Issuer = "LiMS.Tests",
            Audience = "LiMS.Tests.Client",
            SigningKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("a-long-test-only-signing-key-32-bytes")),
            AccessTokenMinutes = 15
        };
        var account = new Account
        {
            AccountId = 42,
            Username = "test-admin",
            PasswordHash = string.Empty,
            Email = "admin@example.test",
            FullName = "Test Admin",
            Role = AccountRoles.Admin
        };
        account.PasswordHash = new PasswordHasher<Account>().HashPassword(account, "unused");
        var service = new TokenService(Options.Create(options));

        var (token, expiresAt) = service.CreateToken(account);
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        var principal = new JwtSecurityTokenHandler().ValidateToken(token, validation, out var validatedToken);

        Assert.IsType<JwtSecurityToken>(validatedToken);
        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("test-admin", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal(AccountRoles.Admin, principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal(options.Issuer, ((JwtSecurityToken)validatedToken).Issuer);
        Assert.Equal(options.Audience, ((JwtSecurityToken)validatedToken).Audiences.Single());
        Assert.InRange(expiresAt, DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
    }
}
