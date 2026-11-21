using LiMS.Api.Contracts;
using LiMS.Api.Data;
using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LiMS.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_returns_token_for_valid_username_and_password()
    {
        await using var db = CreateDatabase();
        var hasher = new PasswordHasher<Account>();
        var account = CreateAccount(hasher, "reader-1", "reader@example.test", "correct-password");
        account.Reader = new Reader
        {
            CardNo = "CARD-1",
            CardIssueDate = new DateOnly(2026, 1, 1),
            CardExpiryDate = new DateOnly(2027, 1, 1),
            CardStatus = RecordStatuses.Active,
            BorrowLimit = 5
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(service => service.CreateToken(It.IsAny<Account>()))
            .Returns(("signed-token", expiresAt));
        var service = new AuthService(db, hasher, tokenService.Object);

        var result = await service.LoginAsync(
            new LoginRequest { Username = " reader@example.test ", Password = "correct-password" },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(account.AccountId, result.Id);
        Assert.Equal("CARD-1", result.StudentCode);
        Assert.Equal("signed-token", result.AccessToken);
        Assert.Equal(expiresAt, result.ExpiresAt);
        tokenService.Verify(item => item.CreateToken(It.Is<Account>(value => value.AccountId == account.AccountId)), Times.Once);
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData("")]
    public async Task LoginAsync_rejects_invalid_password(string password)
    {
        await using var db = CreateDatabase();
        var hasher = new PasswordHasher<Account>();
        db.Accounts.Add(CreateAccount(hasher, "reader-2", "reader2@example.test", "correct-password"));
        await db.SaveChangesAsync();
        var tokenService = new Mock<ITokenService>(MockBehavior.Strict);
        var service = new AuthService(db, hasher, tokenService.Object);

        var result = await service.LoginAsync(
            new LoginRequest { Username = "reader-2", Password = password },
            CancellationToken.None);

        Assert.Null(result);
        tokenService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LoginAsync_rejects_inactive_account()
    {
        await using var db = CreateDatabase();
        var hasher = new PasswordHasher<Account>();
        var account = CreateAccount(hasher, "disabled", "disabled@example.test", "correct-password");
        account.IsActive = false;
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var tokenService = new Mock<ITokenService>(MockBehavior.Strict);
        var service = new AuthService(db, hasher, tokenService.Object);

        var result = await service.LoginAsync(
            new LoginRequest { Username = "disabled", Password = "correct-password" },
            CancellationToken.None);

        Assert.Null(result);
        tokenService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ChangePasswordAsync_updates_hash_and_accepts_new_password()
    {
        await using var db = CreateDatabase();
        var hasher = new PasswordHasher<Account>();
        var account = CreateAccount(hasher, "reader-3", "reader3@example.test", "old-password");
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var service = new AuthService(db, hasher, Mock.Of<ITokenService>());

        var changed = await service.ChangePasswordAsync(
            account.AccountId,
            new ChangePasswordRequest { CurrentPassword = "old-password", NewPassword = "new-password" },
            CancellationToken.None);

        Assert.True(changed);
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(account, account.PasswordHash, "new-password"));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(account, account.PasswordHash, "old-password"));
    }

    [Fact]
    public async Task ChangePasswordAsync_returns_false_for_wrong_current_password()
    {
        await using var db = CreateDatabase();
        var hasher = new PasswordHasher<Account>();
        var account = CreateAccount(hasher, "reader-4", "reader4@example.test", "old-password");
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var originalHash = account.PasswordHash;
        var service = new AuthService(db, hasher, Mock.Of<ITokenService>());

        var changed = await service.ChangePasswordAsync(
            account.AccountId,
            new ChangePasswordRequest { CurrentPassword = "incorrect", NewPassword = "new-password" },
            CancellationToken.None);

        Assert.False(changed);
        Assert.Equal(originalHash, account.PasswordHash);
    }

    private static LiMSDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<LiMSDbContext>()
            .UseInMemoryDatabase($"{nameof(AuthServiceTests)}-{Guid.NewGuid():N}")
            .Options;
        return new LiMSDbContext(options);
    }

    private static Account CreateAccount(
        IPasswordHasher<Account> hasher,
        string username,
        string email,
        string password)
    {
        var account = new Account
        {
            Username = username,
            PasswordHash = string.Empty,
            Email = email,
            FullName = username,
            Role = AccountRoles.Borrower
        };
        account.PasswordHash = hasher.HashPassword(account, password);
        return account;
    }
}
