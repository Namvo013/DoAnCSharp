using LiMS.Api.Contracts;
using LiMS.Api.Models;
using LiMS.Api.Services;

namespace LiMS.Tests;

public sealed class AdminCategoryManagementTests
{
    [Fact]
    public async Task Category_crud_normalizes_names_and_prevents_deleting_categories_in_use()
    {
        await using var database = TestDatabase.Create();
        var service = database.CreateAdminService();

        var created = await service.CreateCategoryAsync(
            new CreateCategoryRequest { Name = "  History  " },
            actorId: 1,
            CancellationToken.None);

        Assert.Equal("History", created.Name);
        Assert.Equal(0, created.BookCount);
        Assert.Single(await service.GetCategoriesAsync(CancellationToken.None));

        await database.Context.Books.AddAsync(new Book
        {
            CategoryId = created.Id,
            Category = await database.Context.Categories.FindAsync(created.Id) ?? throw new InvalidOperationException(),
            Title = "Library History",
            Author = "A. Writer"
        });
        await database.Context.SaveChangesAsync();

        var renamed = await service.UpdateCategoryAsync(
            created.Id,
            new UpdateCategoryRequest { Name = "  Library History  " },
            actorId: 1,
            CancellationToken.None);

        Assert.Equal("Library History", renamed.Name);
        Assert.Equal(1, renamed.BookCount);
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.DeleteCategoryAsync(created.Id, 1, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteCategoryAsync_removes_an_unused_category()
    {
        await using var database = TestDatabase.Create();
        var service = database.CreateAdminService();
        var created = await service.CreateCategoryAsync(
            new CreateCategoryRequest { Name = "Unused" },
            actorId: 1,
            CancellationToken.None);

        await service.DeleteCategoryAsync(created.Id, 1, CancellationToken.None);

        Assert.Empty(await service.GetCategoriesAsync(CancellationToken.None));
    }
}
