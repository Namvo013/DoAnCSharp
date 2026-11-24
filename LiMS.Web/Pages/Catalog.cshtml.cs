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

    [BindProperty(SupportsGet = true), Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    public IReadOnlyList<ApiBook> Books { get; private set; } = [];
    public IReadOnlyList<string> Categories { get; private set; } = [];
    public string? SearchError { get; private set; }
    public int TotalCount { get; private set; }
    public int PageSize { get; } = 24;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public string? GetAbsoluteBookCoverUrl(string? coverImageUrl) =>
        libraryApi.GetAbsoluteBookCoverUrl(coverImageUrl);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("StudentCode")))
            return RedirectToPage("/Index");

        if (ModelState.TryGetValue(nameof(PageNumber), out var pageState) && pageState.Errors.Count > 0
            || Availability is not ("" or "available" or "unavailable")
            || Sort is not ("newest" or "title"))
            return BadRequest(ModelState);

        if (Query is { Length: > 100 })
        {
            SearchError = "Từ khóa tìm kiếm không được vượt quá 100 ký tự.";
            Query = Query[..100];
        }

        Categories = await libraryApi.GetBookCategoriesAsync(cancellationToken);
        var result = await LoadPageAsync(cancellationToken);
        TotalCount = result.TotalCount;
        if (PageNumber > TotalPages)
        {
            PageNumber = TotalPages;
            result = await LoadPageAsync(cancellationToken);
            TotalCount = result.TotalCount;
        }
        Books = result.Items;
        return Page();
    }

    private Task<ApiPagedResponse<ApiBook>> LoadPageAsync(CancellationToken cancellationToken) =>
        libraryApi.GetBooksPageAsync(
            Query?.Trim(),
            Category,
            Availability,
            Sort,
            PageNumber,
            PageSize,
            cancellationToken);
}
