using LiMS.Api.Data;
using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LiMS.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(LiMSDbContext context) => Context = context;

    public LiMSDbContext Context { get; }

    public static TestDatabase Create() => new(new LiMSDbContext(
        new DbContextOptionsBuilder<LiMSDbContext>()
            .UseInMemoryDatabase($"LiMS.Tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options));

    public async Task<(Account Account, Reader Reader)> AddReaderAsync(
        string username = "reader",
        string? cardNo = null,
        bool isActive = true,
        string cardStatus = RecordStatuses.Active,
        DateOnly? expiryDate = null,
        short borrowLimit = 5)
    {
        var account = CreateAccount(username, AccountRoles.Borrower, isActive);
        var reader = new Reader
        {
            Account = account,
            CardNo = cardNo ?? $"CARD-{username}",
            CardIssueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1),
            CardExpiryDate = expiryDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            CardStatus = cardStatus,
            BorrowLimit = borrowLimit
        };
        Context.Readers.Add(reader);
        await Context.SaveChangesAsync();
        return (account, reader);
    }

    public async Task<(Account Account, Librarian Librarian)> AddLibrarianAsync(
        string username = "librarian",
        string role = AccountRoles.Admin)
    {
        var account = CreateAccount(username, role);
        var librarian = new Librarian { Account = account, StaffCode = $"STAFF-{Guid.NewGuid():N}"[..17] };
        Context.Librarians.Add(librarian);
        await Context.SaveChangesAsync();
        return (account, librarian);
    }

    public async Task<(Category Category, Book Book, BookCopy[] Copies)> AddBookAsync(
        string title = "Test book",
        int copyCount = 1,
        string copyStatus = RecordStatuses.Available,
        bool isActive = true)
    {
        var category = new Category { Name = $"Category-{Guid.NewGuid():N}" };
        var book = new Book
        {
            Category = category,
            Title = title,
            Author = "Test author",
            IsActive = isActive
        };
        var copies = Enumerable.Range(1, copyCount)
            .Select(index => new BookCopy
            {
                Barcode = $"BC-{Guid.NewGuid():N}-{index}",
                Status = copyStatus
            })
            .ToArray();
        foreach (var copy in copies)
            book.Copies.Add(copy);
        Context.Books.Add(book);
        await Context.SaveChangesAsync();
        return (category, book, copies);
    }

    public async Task<Loan> AddLoanAsync(
        Reader reader,
        BookCopy copy,
        Librarian librarian,
        DateOnly? dueDate = null,
        DateTime? returnDate = null,
        short renewCount = 0,
        string status = "Borrowed")
    {
        var loan = new Loan
        {
            Reader = reader,
            Copy = copy,
            Librarian = librarian,
            BorrowDate = DateTime.UtcNow.AddDays(-1),
            DueDate = dueDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
            ReturnDate = returnDate,
            RenewCount = renewCount,
            Status = status
        };
        Context.Loans.Add(loan);
        await Context.SaveChangesAsync();
        return loan;
    }

    public async Task<Reservation> AddReservationAsync(
        Reader reader,
        Book book,
        string status = RecordStatuses.Pending,
        DateTime? createdAt = null,
        DateTime? expireAt = null)
    {
        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            Reader = reader,
            Book = book,
            CreatedAt = createdAt ?? now,
            ExpireAt = expireAt ?? now.AddDays(5),
            Status = status
        };
        Context.Reservations.Add(reservation);
        await Context.SaveChangesAsync();
        return reservation;
    }

    public AdminService CreateAdminService(
        IReservationQueueService? reservationQueue = null,
        IPasswordHasher<Account>? passwordHasher = null) =>
        new(
            Context,
            passwordHasher ?? new PasswordHasher<Account>(),
            reservationQueue ?? new ReservationQueueService(Context));

    public ReaderService CreateReaderService(IReservationQueueService? reservationQueue = null) =>
        new(Context, reservationQueue ?? new ReservationQueueService(Context));

    public async ValueTask DisposeAsync() => await Context.DisposeAsync();

    private static Account CreateAccount(string username, string role, bool isActive = true)
    {
        var account = new Account
        {
            Username = username,
            PasswordHash = string.Empty,
            Email = $"{username}-{Guid.NewGuid():N}@example.test",
            FullName = $"Name {username}",
            Role = role,
            IsActive = isActive
        };
        account.PasswordHash = new PasswordHasher<Account>().HashPassword(account, "test-password");
        return account;
    }
}
