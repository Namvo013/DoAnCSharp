using LiMS.Api.Data;
using LiMS.Api.Models;
using LiMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Tests;

public sealed class LibraryQueryServiceTests
{
    [Fact]
    public async Task GetPublicBooksAsync_returns_only_active_books_and_does_not_track_entities()
    {
        await using var db = CreateDatabase();
        var category = new Category { Name = "Reference" };
        db.Categories.Add(category);
        db.Books.AddRange(
            new Book { Category = category, Title = "Visible book", Author = "Author A", IsActive = true },
            new Book { Category = category, Title = "Hidden book", Author = "Author B", IsActive = false });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new LibraryQueryService(db);

        var books = await service.GetPublicBooksAsync(CancellationToken.None);

        var book = Assert.Single(books);
        Assert.Equal("Visible book", book.Title);
        Assert.Equal("Reference", book.Category);
        Assert.Empty(db.ChangeTracker.Entries<Book>());
    }

    [Fact]
    public async Task GetPublicBooksPageAsync_filters_sorts_and_reports_total_count()
    {
        await using var db = CreateDatabase();
        var category = new Category { Name = "Reference" };
        db.Categories.Add(category);
        var first = new Book { Category = category, Title = "Alpha", Author = "Writer", IsActive = true };
        first.Copies.Add(new BookCopy { Barcode = "REF-1", Status = RecordStatuses.Available });
        var second = new Book { Category = category, Title = "Beta", Author = "Writer", IsActive = true };
        second.Copies.Add(new BookCopy { Barcode = "REF-2", Status = RecordStatuses.Available });
        var unavailable = new Book { Category = category, Title = "No copy", Author = "Writer", IsActive = true };
        var hidden = new Book { Category = category, Title = "Hidden", Author = "Writer", IsActive = false };
        hidden.Copies.Add(new BookCopy { Barcode = "REF-3", Status = RecordStatuses.Available });
        db.Books.AddRange(first, second, unavailable, hidden);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new LibraryQueryService(db);

        var page = await service.GetPublicBooksPageAsync(
            null, "Reference", "available", "title", 2, 1, CancellationToken.None);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal("Beta", Assert.Single(page.Items).Title);
        Assert.Empty(db.ChangeTracker.Entries<Book>());
    }

    [Fact]
    public async Task GetPublicBookAsync_does_not_return_inactive_books()
    {
        await using var db = CreateDatabase();
        var category = new Category { Name = "Reference" };
        var book = new Book { Category = category, Title = "Hidden", Author = "Writer", IsActive = false };
        db.Books.Add(book);
        await db.SaveChangesAsync();

        var result = await new LibraryQueryService(db).GetPublicBookAsync(book.BookId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBookCoverAsync_returns_null_when_book_has_no_cover()
    {
        await using var db = CreateDatabase();
        var category = new Category { Name = "Reference" };
        db.Categories.Add(category);
        var book = new Book { Category = category, Title = "No cover", Author = "Author" };
        db.Books.Add(book);
        await db.SaveChangesAsync();
        var service = new LibraryQueryService(db);

        var cover = await service.GetBookCoverAsync(book.BookId, CancellationToken.None);

        Assert.Null(cover);
    }

    [Fact]
    public async Task GetBookCoverAsync_returns_image_data_for_existing_cover()
    {
        await using var db = CreateDatabase();
        var category = new Category { Name = "Reference" };
        db.Categories.Add(category);
        var data = new byte[] { 1, 2, 3 };
        var book = new Book
        {
            Category = category,
            Title = "Has cover",
            Author = "Author",
            CoverImageData = data,
            CoverImageContentType = "image/png"
        };
        db.Books.Add(book);
        await db.SaveChangesAsync();
        var service = new LibraryQueryService(db);

        var cover = await service.GetBookCoverAsync(book.BookId, CancellationToken.None);

        Assert.NotNull(cover);
        Assert.Equal(data, cover.Data);
        Assert.Equal("image/png", cover.ContentType);
    }

    private static LiMSDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<LiMSDbContext>()
            .UseInMemoryDatabase($"{nameof(LibraryQueryServiceTests)}-{Guid.NewGuid():N}")
            .Options;
        return new LiMSDbContext(options);
    }
}
