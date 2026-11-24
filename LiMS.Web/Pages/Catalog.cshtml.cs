using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace LiMS.Web.Pages;

public class CatalogModel(LibraryApiClient libraryApi) : PageModel
{
    [BindProperty(SupportsGet = true)]
    [StringLength(100)]
    public string? Query { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Category { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Availability { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Sort { get; set; } = "newest";

    public IReadOnlyList<ApiBook> Books { get; private set; } = [];
    public IReadOnlyList<string> Categories { get; private set; } = [];
    public string? SearchError { get; private set; }

    public string? GetAbsoluteBookCoverUrl(string? coverImageUrl) =>
        libraryApi.GetAbsoluteBookCoverUrl(coverImageUrl);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Index");

        if (Query is { Length: > 100 })
        {
            SearchError = "Từ khóa tìm kiếm không được vượt quá 100 ký tự.";
            Query = Query[..100];
        }
        var searchTerm = Query?.Trim() ?? string.Empty;

        var books = await libraryApi.GetBooksAsync(cancellationToken);
        Categories = books.Select(book => book.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Select(category => category!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        IEnumerable<ApiBook> filtered = books;
        if (searchTerm.Length > 0)
        {
            filtered = filtered.Where(book =>
                (book.Title?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
                || (book.Author?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
                || (book.Isbn?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(Category))
            filtered = filtered.Where(book =>
                string.Equals(book.Category, Category, StringComparison.OrdinalIgnoreCase));

        filtered = Availability switch
        {
            "available" => filtered.Where(book => book.AvailableCopies > 0),
            "unavailable" => filtered.Where(book => book.AvailableCopies == 0),
            _ => filtered
        };
        Books = Sort == "title"
            ? filtered.OrderBy(book => book.Title, StringComparer.CurrentCultureIgnoreCase).ToArray()
            : filtered.OrderByDescending(book => book.PublishedYear ?? 0)
                .ThenBy(book => book.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        return Page();
    }
}
