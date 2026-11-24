using LiMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Tests;

public sealed class AdminServiceReportingTests
{
    [Fact]
    public async Task GetDashboardAsync_returns_zero_totals_and_six_months_for_empty_database()
    {
        await using var database = TestDatabase.Create();

        var dashboard = await database.CreateAdminService().GetDashboardAsync(CancellationToken.None);

        Assert.Equal(0, dashboard.TotalBooks);
        Assert.Equal(0, dashboard.TotalCopies);
        Assert.Equal(0, dashboard.ActiveReaders);
        Assert.Equal(0, dashboard.ActiveLoans);
        Assert.Equal(0m, dashboard.UnpaidFines);
        Assert.Equal(0m, dashboard.CollectedFines);
        Assert.Equal(6, dashboard.MonthlyReports.Count);
        Assert.Empty(dashboard.PopularBooks);
    }

    [Fact]
    public async Task GetDashboardAsync_aggregates_catalog_circulation_reservations_proposals_and_fines()
    {
        await using var database = TestDatabase.Create();
        var (_, activeReader) = await database.AddReaderAsync(username: "active-reader");
        await database.AddReaderAsync(
            username: "inactive-reader",
            cardStatus: RecordStatuses.Inactive,
            isActive: false);
        var (_, activeBook, activeCopies) = await database.AddBookAsync(
            "Active title", copyCount: 2, copyStatus: RecordStatuses.Available);
        var (_, inactiveBook, inactiveCopies) = await database.AddBookAsync(
            "Inactive title", copyCount: 1, isActive: false);
        var staff = await database.AddLibrarianAsync();
        activeCopies[0].Status = RecordStatuses.OnLoan;
        var openLoan = await database.AddLoanAsync(
            activeReader, activeCopies[0], staff.Librarian,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));
        var returnedLoan = await database.AddLoanAsync(
            activeReader, inactiveCopies[0], staff.Librarian,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            DateTime.UtcNow.AddHours(-1),
            status: RecordStatuses.Returned);
        database.Context.Fines.AddRange(
            new Fine
            {
                Loan = openLoan,
                Amount = 100,
                Reason = "Outstanding",
                Status = RecordStatuses.Pending
            },
            new Fine
            {
                Loan = returnedLoan,
                Amount = 25,
                Reason = "Collected",
                Status = RecordStatuses.Paid,
                PaidAt = DateTime.UtcNow.AddMinutes(-10)
            });
        await database.AddReservationAsync(activeReader, activeBook, RecordStatuses.Pending);
        await database.AddReservationAsync(activeReader, inactiveBook, RecordStatuses.ReadyForPickup);
        database.Context.PurchaseProposals.AddRange(
            Proposal(activeReader, "Pending", RecordStatuses.Pending),
            Proposal(activeReader, "Approved", RecordStatuses.Approved),
            Proposal(activeReader, "Rejected", RecordStatuses.Rejected));
        await database.Context.SaveChangesAsync();

        var dashboard = await database.CreateAdminService().GetDashboardAsync(CancellationToken.None);

        Assert.Equal(1, dashboard.TotalBooks);
        Assert.Equal(3, dashboard.TotalCopies);
        Assert.Equal(2, dashboard.AvailableCopies);
        Assert.Equal(1, dashboard.ActiveReaders);
        Assert.Equal(1, dashboard.ActiveLoans);
        Assert.Equal(1, dashboard.OverdueLoans);
        Assert.Equal(2, dashboard.PendingReservations);
        Assert.Equal(1, dashboard.PendingProposals);
        Assert.Equal(1, dashboard.ApprovedProposals);
        Assert.Equal(1, dashboard.RejectedProposals);
        Assert.Equal(100m, dashboard.UnpaidFines);
        Assert.Equal(25m, dashboard.CollectedFines);
        Assert.Contains(dashboard.PopularBooks, item => item.BookId == activeBook.BookId);
        Assert.Equal(6, dashboard.MonthlyReports.Count);
    }

    [Fact]
    public async Task GetActivityLogsAsync_returns_requested_page_in_newest_first_order()
    {
        await using var database = TestDatabase.Create();
        var staff = await database.AddLibrarianAsync();
        var baseTime = DateTime.UtcNow.AddMinutes(-10);
        for (var index = 0; index < 5; index++)
        {
            database.Context.ActivityLogs.Add(new ActivityLog
            {
                AccountId = staff.Account.AccountId,
                Action = $"Action-{index}",
                TargetTable = "TEST",
                CreatedAt = baseTime.AddMinutes(index)
            });
        }
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var page = await database.CreateAdminService().GetActivityLogsAsync(2, 2, CancellationToken.None);

        Assert.Equal(2, page.Count);
        Assert.Equal(new[] { "Action-2", "Action-1" }, page.Select(item => item.Action));
        Assert.All(page, item => Assert.Equal(staff.Account.Username, item.Username));
        Assert.Empty(database.Context.ChangeTracker.Entries<ActivityLog>());
    }

    [Fact]
    public async Task GetActivityLogsAsync_rejects_invalid_page_or_page_size()
    {
        await using var database = TestDatabase.Create();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdminService().GetActivityLogsAsync(0, 10, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdminService().GetActivityLogsAsync(1, 0, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.CreateAdminService().GetActivityLogsAsync(1, 201, CancellationToken.None));
    }

    [Fact]
    public async Task GetLoansAsync_filters_status_and_does_not_track_entities()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyCount: 2, copyStatus: RecordStatuses.OnLoan);
        var staff = await database.AddLibrarianAsync();
        await database.AddLoanAsync(reader, copies[0], staff.Librarian, status: "Borrowed");
        await database.AddLoanAsync(
            reader, copies[1], staff.Librarian,
            returnDate: DateTime.UtcNow.AddHours(-1),
            status: RecordStatuses.Returned);
        database.Context.ChangeTracker.Clear();

        var loans = await database.CreateAdminService().GetLoansAsync("Borrowed", CancellationToken.None);

        var loan = Assert.Single(loans);
        Assert.Equal("Borrowed", loan.Status);
        Assert.Empty(database.Context.ChangeTracker.Entries<Loan>());
    }

    [Fact]
    public async Task GetFinesAsync_filters_status_and_does_not_track_entities()
    {
        await using var database = TestDatabase.Create();
        var (_, reader) = await database.AddReaderAsync();
        var (_, _, copies) = await database.AddBookAsync(copyCount: 2, copyStatus: RecordStatuses.OnLoan);
        var staff = await database.AddLibrarianAsync();
        var firstLoan = await database.AddLoanAsync(reader, copies[0], staff.Librarian);
        var secondLoan = await database.AddLoanAsync(reader, copies[1], staff.Librarian);
        database.Context.Fines.AddRange(
            new Fine { Loan = firstLoan, Amount = 100, Reason = "Pending", Status = RecordStatuses.Pending },
            new Fine { Loan = secondLoan, Amount = 200, Reason = "Paid", Status = RecordStatuses.Paid });
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var fines = await database.CreateAdminService().GetFinesAsync(RecordStatuses.Paid, CancellationToken.None);

        var fine = Assert.Single(fines);
        Assert.Equal(200m, fine.Amount);
        Assert.Equal(RecordStatuses.Paid, fine.Status);
        Assert.Empty(database.Context.ChangeTracker.Entries<Fine>());
    }

    [Fact]
    public async Task GetConfigsAsync_returns_sorted_values_without_tracking()
    {
        await using var database = TestDatabase.Create();
        database.Context.SystemConfigs.AddRange(
            new SystemConfig { ConfigKey = "Zeta", ConfigValue = "last" },
            new SystemConfig { ConfigKey = "Alpha", ConfigValue = "first" });
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var configs = await database.CreateAdminService().GetConfigsAsync(CancellationToken.None);

        Assert.Equal(new[] { "Alpha", "Zeta" }, configs.Select(item => item.Key));
        Assert.Empty(database.Context.ChangeTracker.Entries<SystemConfig>());
    }

    private static PurchaseProposal Proposal(Reader reader, string title, string status) => new()
    {
        Reader = reader,
        Title = title,
        Author = "Author",
        Reason = "Reason",
        Status = status
    };
}
