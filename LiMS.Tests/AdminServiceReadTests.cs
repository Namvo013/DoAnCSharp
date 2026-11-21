using LiMS.Api.Data;
using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LiMS.Tests;

public sealed class AdminServiceReadTests
{
    [Fact]
    public async Task GetBooksAsync_filters_by_search_and_maps_copy_counts_without_tracking()
    {
        await using var db = CreateDatabase();
        var category = new Category { Name = "History" };
        db.Categories.Add(category);
        var matching = new Book { Category = category, Title = "History of LiMS", Author = "A. Writer" };
        matching.Copies.Add(new BookCopy { Barcode = "H-1", Status = RecordStatuses.Available });
        matching.Copies.Add(new BookCopy { Barcode = "H-2", Status = RecordStatuses.OnLoan });
        db.Books.Add(matching);
        db.Books.Add(new Book { Category = category, Title = "Science", Author = "B. Writer" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = CreateService(db);

        var books = await service.GetBooksAsync("LiMS", CancellationToken.None);

        var result = Assert.Single(books);
        Assert.Equal("History of LiMS", result.Title);
        Assert.Equal(2, result.TotalCopies);
        Assert.Equal(1, result.AvailableCopies);
        Assert.Empty(db.ChangeTracker.Entries<Book>());
    }

    [Fact]
    public async Task GetReadersAsync_filters_by_status_and_returns_no_tracked_entities()
    {
        await using var db = CreateDatabase();
        AddReader(db, "active-reader", "ACTIVE-1", RecordStatuses.Active);
        AddReader(db, "inactive-reader", "INACTIVE-1", RecordStatuses.Inactive);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = CreateService(db);

        var readers = await service.GetReadersAsync(null, RecordStatuses.Active, CancellationToken.None);

        var result = Assert.Single(readers);
        Assert.Equal("ACTIVE-1", result.CardNo);
        Assert.Empty(db.ChangeTracker.Entries<Reader>());
        Assert.Empty(db.ChangeTracker.Entries<Account>());
    }

    [Fact]
    public async Task GetProposalsAsync_maps_owner_and_does_not_track_entities()
    {
        await using var db = CreateDatabase();
        var account = CreateAccount("proposal-reader");
        var reader = new Reader
        {
            Account = account,
            CardNo = "CARD-P",
            CardIssueDate = new DateOnly(2026, 1, 1),
            CardExpiryDate = new DateOnly(2027, 1, 1),
            CardStatus = RecordStatuses.Active,
            BorrowLimit = 5
        };
        db.Readers.Add(reader);
        db.PurchaseProposals.Add(new PurchaseProposal
        {
            Reader = reader,
            Title = "Requested title",
            Author = "Requested author",
            Reason = "Reader request",
            Status = RecordStatuses.Pending
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = CreateService(db);

        var proposals = await service.GetProposalsAsync(RecordStatuses.Pending, CancellationToken.None);

        var proposal = Assert.Single(proposals);
        Assert.Equal("CARD-P", proposal.ReaderCardNo);
        Assert.Equal("Requested title", proposal.Title);
        Assert.Empty(db.ChangeTracker.Entries<PurchaseProposal>());
    }

    private static AdminService CreateService(LiMSDbContext db) =>
        new(db, Mock.Of<IPasswordHasher<Account>>(), Mock.Of<IReservationQueueService>());

    private static LiMSDbContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<LiMSDbContext>()
            .UseInMemoryDatabase($"{nameof(AdminServiceReadTests)}-{Guid.NewGuid():N}")
            .Options);

    private static Account CreateAccount(string username) => new()
    {
        Username = username,
        PasswordHash = "test-hash",
        Email = $"{username}@example.test",
        FullName = username,
        Role = AccountRoles.Borrower
    };

    private static void AddReader(LiMSDbContext db, string username, string cardNo, string status)
    {
        var account = CreateAccount(username);
        db.Readers.Add(new Reader
        {
            Account = account,
            CardNo = cardNo,
            CardIssueDate = new DateOnly(2026, 1, 1),
            CardExpiryDate = new DateOnly(2027, 1, 1),
            CardStatus = status,
            BorrowLimit = 5
        });
    }
}
