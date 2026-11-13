using System.Security.Cryptography;
using LiMS.Api.Models;
using LiMS.Api.Models.Legacy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Data;

public static class DatabaseInitializer
{
    private const string ImportMarker = "LegacyImportCompleted";

    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LiMSDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        await SeedBusinessConfigAsync(db, cancellationToken);
        await SeedBootstrapAdminAsync(
            db,
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>(),
            configuration,
            logger,
            cancellationToken);
        await ImportLegacyDataAsync(
            db,
            scope.ServiceProvider.GetRequiredService<LegacyLiMSDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>(),
            logger,
            cancellationToken);
    }

    private static async Task SeedBusinessConfigAsync(
        LiMSDbContext db,
        CancellationToken cancellationToken)
    {
        var defaults = new Dictionary<string, (string Value, string Description)>
        {
            ["LoanDurationDays"] = ("14", "Số ngày mượn một lần."),
            ["MaxRenewals"] = ("1", "Số lần gia hạn tối đa cho mỗi phiếu mượn."),
            ["ReservationPickupDays"] = ("3", "Số ngày giữ sách sau khi có sách."),
            ["FinePerDay"] = ("5000", "Mức phạt quá hạn mỗi ngày (VND)."),
            ["MaxActiveLoans"] = ("5", "Giới hạn phiếu mượn đang mở khi không có giới hạn riêng trên thẻ.")
        };
        var existingKeys = await db.SystemConfigs.Select(item => item.ConfigKey).ToListAsync(cancellationToken);
        foreach (var (key, value) in defaults)
        {
            if (!existingKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                db.SystemConfigs.Add(new SystemConfig
                {
                    ConfigKey = key,
                    ConfigValue = value.Value,
                    Description = value.Description
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedBootstrapAdminAsync(
        LiMSDbContext db,
        IPasswordHasher<Account> passwordHasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var username = configuration["BootstrapAdmin:Username"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrEmpty(username))
        {
            if (!await db.Accounts.AnyAsync(account => account.Role == AccountRoles.Admin, cancellationToken))
                logger.LogWarning("No administrator exists. Configure BootstrapAdmin:Username and BootstrapAdmin:Password to create one.");
            return;
        }

        if (await db.Accounts.AnyAsync(account => account.Username == username, cancellationToken))
            return;
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set BootstrapAdmin:Password to create the requested administrator.");

        var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant()
            ?? $"{username.ToLowerInvariant()}@library.local";
        var account = new Account
        {
            Username = username,
            PasswordHash = string.Empty,
            Email = email,
            FullName = configuration["BootstrapAdmin:FullName"]?.Trim() ?? "Library Administrator",
            Role = AccountRoles.Admin
        };
        account.PasswordHash = passwordHasher.HashPassword(account, password);
        var librarian = new Librarian
        {
            Account = account,
            StaffCode = configuration["BootstrapAdmin:StaffCode"]?.Trim() ?? "ADM-BOOTSTRAP"
        };
        db.Librarians.Add(librarian);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Bootstrapped administrator account {Username}.", username);
    }

    private static async Task ImportLegacyDataAsync(
        LiMSDbContext db,
        LegacyLiMSDbContext legacy,
        IPasswordHasher<Account> passwordHasher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (await db.SystemConfigs.AnyAsync(item => item.ConfigKey == ImportMarker, cancellationToken))
            return;

        var legacyUsers = await legacy.Users.AsNoTracking().ToListAsync(cancellationToken);
        var legacyBooks = await legacy.Books.AsNoTracking().ToListAsync(cancellationToken);
        var legacyLoans = await legacy.Loans.AsNoTracking().ToListAsync(cancellationToken);
        var legacyReservations = await legacy.Reservations.AsNoTracking().ToListAsync(cancellationToken);
        var legacyNotifications = await legacy.Notifications.AsNoTracking().ToListAsync(cancellationToken);
        var hasLegacyData = legacyUsers.Count > 0
            || legacyBooks.Count > 0
            || legacyLoans.Count > 0
            || legacyReservations.Count > 0
            || legacyNotifications.Count > 0;
        if (!hasLegacyData)
        {
            db.SystemConfigs.Add(new SystemConfig
            {
                ConfigKey = ImportMarker,
                ConfigValue = DateTime.UtcNow.ToString("O"),
                Description = "One-time import marker for the legacy LiMS tables."
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var readersByLegacyId = new Dictionary<int, Reader>();
        var usedEmails = new HashSet<string>(
            await db.Accounts.Select(account => account.Email).ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        foreach (var legacyUser in legacyUsers)
        {
            var username = legacyUser.StudentCode.Trim();
            if (string.IsNullOrWhiteSpace(username))
                continue;

            var account = await db.Accounts
                .Include(item => item.Reader)
                .Include(item => item.Librarian)
                .SingleOrDefaultAsync(item => item.Username == username, cancellationToken);
            if (account is null)
            {
                var email = NormalizeEmail(legacyUser.Email, username, legacyUser.Id, usedEmails);
                account = new Account
                {
                    Username = username,
                    PasswordHash = string.Empty,
                    Email = email,
                    FullName = string.IsNullOrWhiteSpace(legacyUser.FullName) ? username : legacyUser.FullName.Trim(),
                    Role = NormalizeRole(legacyUser.Role),
                    IsActive = legacyUser.IsActive
                };
                var legacyPassword = string.IsNullOrEmpty(legacyUser.PasswordHash)
                    ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    : legacyUser.PasswordHash;
                account.PasswordHash = passwordHasher.HashPassword(account, legacyPassword);
                db.Accounts.Add(account);
            }

            var role = NormalizeRole(account.Role);
            account.Role = role;
            if (role == AccountRoles.Borrower)
            {
                var reader = account.Reader ?? new Reader
                {
                    Account = account,
                    CardNo = username,
                    CardIssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    CardExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(4),
                    CardStatus = legacyUser.IsActive ? RecordStatuses.Active : RecordStatuses.Inactive,
                    BorrowLimit = 5
                };
                if (account.Reader is null)
                    db.Readers.Add(reader);
                readersByLegacyId[legacyUser.Id] = reader;
            }
            else if ((role is AccountRoles.Librarian or AccountRoles.Admin) && account.Librarian is null)
            {
                db.Librarians.Add(new Librarian
                {
                    Account = account,
                    StaffCode = $"LEG-{legacyUser.Id:D6}"
                });
            }
            else if (account.Reader is not null)
            {
                readersByLegacyId[legacyUser.Id] = account.Reader;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var legacyUser in legacyUsers)
        {
            var reader = await db.Readers.Include(item => item.Account)
                .SingleOrDefaultAsync(item => item.CardNo == legacyUser.StudentCode, cancellationToken);
            if (reader is not null)
                readersByLegacyId[legacyUser.Id] = reader;
        }

        var bookIds = new Dictionary<int, int>();
        foreach (var legacyBook in legacyBooks)
        {
            var author = string.IsNullOrWhiteSpace(legacyBook.Author) ? "Chưa cập nhật" : legacyBook.Author.Trim();
            var title = string.IsNullOrWhiteSpace(legacyBook.Title) ? $"Sách cũ {legacyBook.Id}" : legacyBook.Title.Trim();
            var book = await FindMatchingBookAsync(db, legacyBook, title, author, cancellationToken);
            if (book is null)
            {
                var categoryName = string.IsNullOrWhiteSpace(legacyBook.Category)
                    ? "Chưa phân loại"
                    : legacyBook.Category.Trim();
                var category = await db.Categories.SingleOrDefaultAsync(
                    item => item.Name == categoryName,
                    cancellationToken);
                if (category is null)
                {
                    category = new Category { Name = categoryName };
                    db.Categories.Add(category);
                }

                book = new Book
                {
                    Category = category,
                    Title = title,
                    Author = author,
                    Isbn = string.IsNullOrWhiteSpace(legacyBook.Isbn) ? null : legacyBook.Isbn.Trim(),
                    Publisher = string.IsNullOrWhiteSpace(legacyBook.Publisher) ? null : legacyBook.Publisher.Trim(),
                    PublishYear = legacyBook.PublishedYear is >= 1400 and <= 2100
                        ? (short)legacyBook.PublishedYear.Value
                        : null
                };
                db.Books.Add(book);
                await db.SaveChangesAsync(cancellationToken);

                var total = Math.Max(0, legacyBook.TotalCopies);
                var available = Math.Clamp(legacyBook.AvailableCopies, 0, total);
                for (var index = 1; index <= total; index++)
                {
                    db.BookCopies.Add(new BookCopy
                    {
                        BookId = book.BookId,
                        Barcode = $"LEG-{legacyBook.Id:D6}-{index:D4}",
                        Status = index <= available ? RecordStatuses.Available : RecordStatuses.Unavailable
                    });
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            bookIds[legacyBook.Id] = book.BookId;
        }

        int? migrationLibrarianId = null;
        if (legacyLoans.Count > 0)
        {
            var librarians = await db.Librarians.OrderBy(item => item.LibrarianId).ToListAsync(cancellationToken);
            migrationLibrarianId = librarians.FirstOrDefault()?.LibrarianId
                ?? await CreateImportLibrarianAsync(db, passwordHasher, cancellationToken);
        }

        foreach (var legacyLoan in legacyLoans)
        {
            if (!readersByLegacyId.TryGetValue(legacyLoan.UserId, out var reader)
                || !bookIds.TryGetValue(legacyLoan.BookId, out var bookId))
            {
                logger.LogWarning("Skipped legacy loan {LegacyLoanId} because its reader or book could not be mapped.", legacyLoan.Id);
                continue;
            }
            if (migrationLibrarianId is null)
                throw new InvalidOperationException("Legacy loans require an import librarian account.");

            var active = legacyLoan.ReturnedDate is null;
            var copyQuery = db.BookCopies.Where(item => item.BookId == bookId);
            if (active)
                copyQuery = copyQuery.Where(item => item.Status != RecordStatuses.OnLoan);
            var copy = await copyQuery.FirstOrDefaultAsync(cancellationToken);
            if (copy is null)
            {
                copy = new BookCopy
                {
                    BookId = bookId,
                    Barcode = $"LEG-{legacyLoan.BookId:D6}-LOAN-{legacyLoan.Id:D6}",
                    Status = RecordStatuses.Unavailable
                };
                db.BookCopies.Add(copy);
            }

            if (active)
                copy.Status = RecordStatuses.OnLoan;
            var dueDate = DateOnly.FromDateTime(legacyLoan.DueDate);
            var status = !active
                ? RecordStatuses.Returned
                : dueDate < DateOnly.FromDateTime(DateTime.UtcNow) ? RecordStatuses.Overdue : "Borrowed";
            db.Loans.Add(new Loan
            {
                ReaderId = reader.ReaderId,
                Copy = copy,
                LibrarianId = migrationLibrarianId.Value,
                BorrowDate = DateTime.SpecifyKind(legacyLoan.BorrowedDate, DateTimeKind.Utc),
                DueDate = dueDate,
                ReturnDate = legacyLoan.ReturnedDate is null
                    ? null
                    : DateTime.SpecifyKind(legacyLoan.ReturnedDate.Value, DateTimeKind.Utc),
                RenewCount = (short)Math.Clamp(legacyLoan.RenewalCount, 0, short.MaxValue),
                Status = status
            });
        }

        foreach (var legacyReservation in legacyReservations)
        {
            if (!readersByLegacyId.TryGetValue(legacyReservation.UserId, out var reader)
                || !bookIds.TryGetValue(legacyReservation.BookId, out var bookId))
            {
                logger.LogWarning("Skipped legacy reservation {LegacyReservationId} because its reader or book could not be mapped.", legacyReservation.Id);
                continue;
            }

            var status = legacyReservation.Status is "Pending" or "ReadyForPickup"
                ? legacyReservation.Status
                : legacyReservation.Status == "Cancelled" ? "Cancelled" : "Expired";
            db.Reservations.Add(new Reservation
            {
                ReaderId = reader.ReaderId,
                BookId = bookId,
                CreatedAt = DateTime.SpecifyKind(legacyReservation.CreatedDate, DateTimeKind.Utc),
                ExpireAt = DateTime.SpecifyKind(legacyReservation.ExpiryDate, DateTimeKind.Utc),
                Status = status
            });
        }

        foreach (var notification in legacyNotifications)
        {
            if (!readersByLegacyId.TryGetValue(notification.UserId, out var reader))
                continue;
            db.EmailLogs.Add(new EmailLog
            {
                ReaderId = reader.ReaderId,
                EmailType = "LegacyNotification",
                SentAt = DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc),
                SendStatus = "Sent"
            });
        }

        db.SystemConfigs.Add(new SystemConfig
        {
            ConfigKey = ImportMarker,
            ConfigValue = DateTime.UtcNow.ToString("O"),
            Description = "One-time import marker for the legacy LiMS tables."
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Imported legacy LiMS data: {Users} users, {Books} books, {Loans} loans, {Reservations} reservations, {Notifications} notifications.",
            legacyUsers.Count,
            bookIds.Count,
            legacyLoans.Count,
            legacyReservations.Count,
            legacyNotifications.Count);
    }

    private static async Task<Book?> FindMatchingBookAsync(
        LiMSDbContext db,
        LegacyBook legacyBook,
        string title,
        string author,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(legacyBook.Isbn))
        {
            var byIsbn = await db.Books.SingleOrDefaultAsync(
                item => item.Isbn == legacyBook.Isbn,
                cancellationToken);
            if (byIsbn is not null)
                return byIsbn;
        }

        return await db.Books.SingleOrDefaultAsync(
            item => item.Title == title && item.Author == author,
            cancellationToken);
    }

    private static async Task<int> CreateImportLibrarianAsync(
        LiMSDbContext db,
        IPasswordHasher<Account> passwordHasher,
        CancellationToken cancellationToken)
    {
        var username = "LiMS-import";
        var account = new Account
        {
            Username = username,
            PasswordHash = string.Empty,
            Email = "lims-import@library.invalid",
            FullName = "Legacy Data Import",
            Role = AccountRoles.Librarian,
            IsActive = false
        };
        account.PasswordHash = passwordHasher.HashPassword(
            account,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var librarian = new Librarian { Account = account, StaffCode = "SYS-IMPORT" };
        db.Librarians.Add(librarian);
        await db.SaveChangesAsync(cancellationToken);
        return librarian.LibrarianId;
    }

    private static string NormalizeRole(string? role) =>
        role?.Trim().ToLowerInvariant() switch
        {
            "admin" => AccountRoles.Admin,
            "librarian" => AccountRoles.Librarian,
            _ => AccountRoles.Borrower
        };

    private static string NormalizeEmail(
        string? email,
        string username,
        int legacyId,
        HashSet<string> usedEmails)
    {
        var candidate = string.IsNullOrWhiteSpace(email)
            ? $"{username}@legacy.invalid"
            : email.Trim().ToLowerInvariant();
        if (!usedEmails.Add(candidate))
        {
            candidate = $"{username}.{legacyId}@legacy.invalid";
            if (!usedEmails.Add(candidate))
                candidate = $"legacy-{legacyId}@library.invalid";
        }

        return candidate;
    }
}
