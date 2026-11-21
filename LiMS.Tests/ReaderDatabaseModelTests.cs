using LiMS.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LiMS.Tests;

public sealed class ReaderDatabaseModelTests
{
    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<LiMSDbContext>()
            .UseInMemoryDatabase($"schema-{Guid.NewGuid():N}")
            .Options;
        using var context = new LiMSDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Reader_model_contains_card_date_and_borrow_limit_checks()
    {
        var entity = CreateModel().FindEntityType("LiMS.Api.Models.Reader");

        Assert.NotNull(entity);
        var constraints = entity.GetCheckConstraints().ToDictionary(item => item.Name!, item => item.Sql);
        Assert.Equal("[CardExpiryDate] >= [CardIssueDate]", constraints["CK_READER_CardDateRange"]);
        Assert.Equal("[BorrowLimit] >= 0", constraints["CK_READER_BorrowLimit"]);
    }

    [Fact]
    public void Loan_and_reservation_models_reject_invalid_dates_and_negative_renewals()
    {
        var model = CreateModel();
        var loanChecks = model.FindEntityType("LiMS.Api.Models.Loan")!
            .GetCheckConstraints().ToDictionary(item => item.Name!, item => item.Sql);
        var reservationChecks = model.FindEntityType("LiMS.Api.Models.Reservation")!
            .GetCheckConstraints().ToDictionary(item => item.Name!, item => item.Sql);

        Assert.Equal("[DueDate] >= CONVERT(date, [BorrowDate])", loanChecks["CK_LOAN_DueDateAfterBorrowDate"]);
        Assert.Equal("[RenewCount] >= 0", loanChecks["CK_LOAN_RenewCount"]);
        Assert.Equal("[ExpireAt] >= [CreatedAt]", reservationChecks["CK_RESERVATION_ExpiryAfterCreated"]);
    }

    [Fact]
    public void Reader_workload_indexes_match_history_queue_and_notification_ordering()
    {
        var model = CreateModel();

        AssertIndex(model, "LiMS.Api.Models.Loan", "ReaderId", "ReturnDate", "DueDate");
        AssertIndex(model, "LiMS.Api.Models.Reservation", "BookId", "Status", "ExpireAt", "CreatedAt");
        AssertIndex(model, "LiMS.Api.Models.ReaderNotification", "ReaderId", "IsRead", "CreatedAt");
    }

    private static void AssertIndex(IModel model, string entityName, params string[] properties)
    {
        var entity = model.FindEntityType(entityName);
        Assert.NotNull(entity);
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(properties));
    }
}
