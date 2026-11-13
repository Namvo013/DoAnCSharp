using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LiMS.Admin.Models;

namespace LiMS.Admin.Services;

public sealed class AdminApiClient : IDisposable
{
    private readonly HttpClient _client;

    private AdminApiClient(HttpClient client) => _client = client;

    public void Dispose() => _client.Dispose();

    public static async Task<(AdminApiClient Client, LoginResponse User)> SignInAsync(
        Uri baseAddress,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = baseAddress };
        using var response = await client.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequest(username, password),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync(response, cancellationToken);

        var user = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("API đăng nhập trả về dữ liệu rỗng.");
        if (string.IsNullOrWhiteSpace(user.AccessToken))
            throw new InvalidDataException("API đăng nhập không trả về mã truy cập.");

        var authenticatedClient = new HttpClient { BaseAddress = baseAddress };
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", user.AccessToken);
        return (new AdminApiClient(authenticatedClient), user);
    }

    public Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken) =>
        GetAsync<DashboardResponse>("api/admin/dashboard", cancellationToken);

    public Task<MessageResponse> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<MessageResponse>("api/auth/change-password", request, cancellationToken);

    public Task<IReadOnlyList<BookResponse>> GetBooksAsync(
        string? search,
        CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<BookResponse>>(
            string.IsNullOrWhiteSpace(search)
                ? "api/admin/books"
                : $"api/admin/books?search={Uri.EscapeDataString(search)}",
            cancellationToken);

    public Task<BookResponse> CreateBookAsync(CreateBookRequest request, CancellationToken cancellationToken) =>
        PostAsync<BookResponse>("api/admin/books", request, cancellationToken);

    public Task<BookResponse> UpdateBookAsync(int id, UpdateBookRequest request, CancellationToken cancellationToken) =>
        PutAsync<BookResponse>($"api/admin/books/{id}", request, cancellationToken);

    public Task<IReadOnlyList<BookCopyResponse>> GetCopiesAsync(int bookId, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<BookCopyResponse>>($"api/admin/books/{bookId}/copies", cancellationToken);

    public Task<MessageResponse> AddCopiesAsync(
        int bookId,
        CreateCopiesRequest request,
        CancellationToken cancellationToken) =>
        PostAsync<MessageResponse>($"api/admin/books/{bookId}/copies", request, cancellationToken);

    public Task<MessageResponse> UpdateCopyStatusAsync(
        int copyId,
        CopyStatusRequest request,
        CancellationToken cancellationToken) =>
        PutAsync<MessageResponse>($"api/admin/copies/{copyId}/status", request, cancellationToken);

    public Task<IReadOnlyList<ReaderResponse>> GetReadersAsync(string? search, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<ReaderResponse>>(
            string.IsNullOrWhiteSpace(search)
                ? "api/admin/readers"
                : $"api/admin/readers?search={Uri.EscapeDataString(search)}",
            cancellationToken);

    public Task<ReaderResponse> CreateReaderAsync(CreateReaderRequest request, CancellationToken cancellationToken) =>
        PostAsync<ReaderResponse>("api/admin/readers", request, cancellationToken);

    public Task<ReaderResponse> UpdateReaderAsync(
        int id,
        UpdateReaderRequest request,
        CancellationToken cancellationToken) =>
        PutAsync<ReaderResponse>($"api/admin/readers/{id}", request, cancellationToken);

    public Task<IReadOnlyList<LoanResponse>> GetLoansAsync(string? status, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<LoanResponse>>(
            string.IsNullOrWhiteSpace(status)
                ? "api/admin/loans"
                : $"api/admin/loans?status={Uri.EscapeDataString(status)}",
            cancellationToken);

    public Task<LoanResponse> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken) =>
        PostAsync<LoanResponse>("api/admin/loans/checkout", request, cancellationToken);

    public Task<LoanResponse> ReturnAsync(ReturnRequest request, CancellationToken cancellationToken) =>
        PostAsync<LoanResponse>("api/admin/loans/return", request, cancellationToken);

    public Task<LoanResponse> RenewAsync(long id, CancellationToken cancellationToken) =>
        PostAsync<LoanResponse>($"api/admin/loans/{id}/renew", new { }, cancellationToken);

    public Task<IReadOnlyList<FineResponse>> GetFinesAsync(string? status, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<FineResponse>>(
            string.IsNullOrWhiteSpace(status)
                ? "api/admin/fines"
                : $"api/admin/fines?status={Uri.EscapeDataString(status)}",
            cancellationToken);

    public Task<FineResponse> CollectFineAsync(
        long id,
        CollectFineRequest request,
        CancellationToken cancellationToken) =>
        PutAsync<FineResponse>($"api/admin/fines/{id}/collect", request, cancellationToken);

    public Task<IReadOnlyList<ProposalResponse>> GetProposalsAsync(
        string? status,
        CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<ProposalResponse>>(
            string.IsNullOrWhiteSpace(status)
                ? "api/admin/proposals"
                : $"api/admin/proposals?status={Uri.EscapeDataString(status)}",
            cancellationToken);

    public Task<ProposalResponse> ReviewProposalAsync(
        int id,
        ReviewProposalRequest request,
        CancellationToken cancellationToken) =>
        PutAsync<ProposalResponse>($"api/admin/proposals/{id}/review", request, cancellationToken);

    public Task<IReadOnlyList<SystemConfigResponse>> GetSettingsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<SystemConfigResponse>>("api/admin/settings", cancellationToken);

    public Task<SystemConfigResponse> UpdateSettingAsync(
        string key,
        UpdateSystemConfigRequest request,
        CancellationToken cancellationToken) =>
        PutAsync<SystemConfigResponse>(
            $"api/admin/settings/{Uri.EscapeDataString(key)}",
            request,
            cancellationToken);

    public Task<IReadOnlyList<ActivityLogResponse>> GetActivityLogsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<ActivityLogResponse>>("api/admin/activity-logs?page=1&pageSize=100", cancellationToken);

    private async Task<TResponse> GetAsync<TResponse>(string uri, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(uri, cancellationToken);
        return await ReadResponseAsync<TResponse>(response, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TResponse>(
        string uri,
        object body,
        CancellationToken cancellationToken)
    {
        using var response = await _client.PostAsJsonAsync(uri, body, cancellationToken);
        return await ReadResponseAsync<TResponse>(response, cancellationToken);
    }

    private async Task<TResponse> PutAsync<TResponse>(
        string uri,
        object body,
        CancellationToken cancellationToken)
    {
        using var response = await _client.PutAsJsonAsync(uri, body, cancellationToken);
        return await ReadResponseAsync<TResponse>(response, cancellationToken);
    }

    private static async Task<TResponse> ReadResponseAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("API trả về dữ liệu rỗng.");
    }

    private static async Task<AdminApiException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken: cancellationToken);
        var message = problem?.Detail
            ?? problem?.Title
            ?? $"Yêu cầu thất bại với mã HTTP {(int)response.StatusCode}.";
        return new AdminApiException(response.StatusCode, message);
    }

    public sealed class AdminApiException(HttpStatusCode statusCode, string message)
        : Exception(message)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }
}
