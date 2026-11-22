using LiMS.Api.Contracts;
using LiMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Tests;

public sealed class ConfigValidationBoundaryTests
{
    [Theory]
    [InlineData("LoanDurationDays", "1")]
    [InlineData("LoanDurationDays", "365")]
    [InlineData("MaxRenewals", "0")]
    [InlineData("MaxRenewals", "10")]
    [InlineData("ReservationPickupDays", "1")]
    [InlineData("ReservationPickupDays", "30")]
    [InlineData("MaxActiveLoans", "1")]
    [InlineData("MaxActiveLoans", "100")]
    [InlineData("FinePerDay", "0")]
    [InlineData("FinePerDay", "1000000")]
    public async Task UpdateConfigAsync_accepts_each_documented_minimum_and_maximum(
        string key,
        string value)
    {
        await using var database = TestDatabase.Create();

        var saved = await database.CreateAdminService().UpdateConfigAsync(
            key,
            new UpdateSystemConfigRequest { Value = value },
            actorId: 3,
            CancellationToken.None);

        Assert.Equal(value, saved.Value);
        Assert.Equal(value, (await database.Context.SystemConfigs.SingleAsync()).ConfigValue);
    }

    [Theory]
    [InlineData("LoanDurationDays", "0")]
    [InlineData("LoanDurationDays", "366")]
    [InlineData("LoanDurationDays", "-1")]
    [InlineData("LoanDurationDays", "14.5")]
    [InlineData("MaxRenewals", "-1")]
    [InlineData("MaxRenewals", "11")]
    [InlineData("ReservationPickupDays", "0")]
    [InlineData("ReservationPickupDays", "31")]
    [InlineData("MaxActiveLoans", "0")]
    [InlineData("MaxActiveLoans", "101")]
    [InlineData("FinePerDay", "-0.01")]
    [InlineData("FinePerDay", "1000000.01")]
    [InlineData("FinePerDay", "1e4")]
    [InlineData("FinePerDay", "not-a-number")]
    [InlineData("MaxRenewals", "")]
    [InlineData("MaxRenewals", "   ")]
    public async Task UpdateConfigAsync_rejects_invalid_values_without_persisting(
        string key,
        string value)
    {
        await using var database = TestDatabase.Create();

        await Assert.ThrowsAsync<ArgumentException>(() => database.CreateAdminService().UpdateConfigAsync(
            key,
            new UpdateSystemConfigRequest { Value = value },
            actorId: 3,
            CancellationToken.None));

        Assert.Empty(await database.Context.SystemConfigs.ToListAsync());
        Assert.Empty(await database.Context.ActivityLogs.ToListAsync());
    }

    [Theory]
    [InlineData(" loandurationdays ")]
    [InlineData("MAXRENEWALS")]
    [InlineData("ReservationPickupDays")]
    [InlineData("MaxActiveLoans")]
    [InlineData("FinePerDay")]
    public async Task UpdateConfigAsync_keeps_supported_key_case_normalization(string key)
    {
        await using var database = TestDatabase.Create();
        var normalizedKey = key.Trim();

        var result = await database.CreateAdminService().UpdateConfigAsync(
            normalizedKey,
            new UpdateSystemConfigRequest { Value = "1" },
            actorId: 3,
            CancellationToken.None);

        Assert.Equal(
            normalizedKey,
            result.Key,
            ignoreCase: true);
    }

    [Fact]
    public async Task UpdateConfigAsync_does_not_accept_key_with_leading_or_trailing_spaces()
    {
        await using var database = TestDatabase.Create();

        await Assert.ThrowsAsync<ArgumentException>(() => database.CreateAdminService().UpdateConfigAsync(
            " LoanDurationDays ",
            new UpdateSystemConfigRequest { Value = "14" },
            actorId: 3,
            CancellationToken.None));

        Assert.Empty(await database.Context.SystemConfigs.ToListAsync());
    }
}
