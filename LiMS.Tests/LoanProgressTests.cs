using LiMS.Web.Models;
using LiMS.Web.Pages;

namespace LiMS.Tests;

public sealed class LoanProgressTests
{
    [Fact]
    public void LoanProgressPercentage_calculates_elapsed_time_and_clamps_boundaries()
    {
        var borrowed = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var due = borrowed.AddDays(10);
        var loan = new ApiLoan(1, 1, 1, borrowed, due, null, "Borrowed", 0, null);

        Assert.Equal(47, HomeModel.LoanProgressPercentage(loan, borrowed.AddDays(5)));
        Assert.Equal(0, HomeModel.LoanProgressPercentage(loan, borrowed.AddDays(-1)));
        Assert.Equal(100, HomeModel.LoanProgressPercentage(loan, due.AddDays(2)));
    }
}
