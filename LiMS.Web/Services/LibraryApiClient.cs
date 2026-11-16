using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LiMS.Web.Models;

namespace LiMS.Web.Services;

public sealed class LibraryApiClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("LiMS.Api");

    public async Task<ApiUser?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        using var message = CreateRequest(HttpMethod.Post, "api/auth/login");
        message.Content = JsonContent.Create(request);
        using var response = await _client.SendAsync(message, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiUser>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("API đăng nhập trả về dữ liệu rỗng.");
    }

    public async Task<IReadOnlyList<ApiBook>> GetBooksAsync(CancellationToken cancellationToken)
    {
        using var message = CreateRequest(HttpMethod.Get, "api/library/books");
        using var response = await _client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ApiBook>>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("API danh sách sách trả về dữ liệu rỗng.");
    }

    public async Task<IReadOnlyList<ApiLoan>> GetLoansAsync(
        string studentCode,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Get,
            $"api/library/loans/{Uri.EscapeDataString(studentCode)}");
        using var response = await _client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ApiLoan>>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("API phiếu mượn trả về dữ liệu rỗng.");
    }

    public async Task<IReadOnlyList<ApiReservation>> GetReservationsAsync(
        string studentCode,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Get,
            $"api/library/reservations/{Uri.EscapeDataString(studentCode)}");
        using var response = await _client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ApiReservation>>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("API phiếu đặt giữ trả về dữ liệu rỗng.");
    }

    public async Task<IReadOnlyList<ApiNotification>> GetNotificationsAsync(
        string studentCode,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Get,
            $"api/library/notifications/{Uri.EscapeDataString(studentCode)}");
        using var response = await _client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ApiNotification>>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("API thông báo trả về dữ liệu rỗng.");
    }

    public async Task<CreatedReservation> CreateReservationAsync(
        int bookId,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(HttpMethod.Post, "api/library/reservations");
        message.Content = JsonContent.Create(new { BookId = bookId });
        using var response = await _client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<CreatedReservation>(
                cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("API đặt giữ trả về dữ liệu rỗng.");
    }

    public async Task CancelReservationAsync(long reservationId, CancellationToken cancellationToken)
    {
        using var message = CreateRequest(HttpMethod.Delete, $"api/library/reservations/{reservationId}");
        using var response = await _client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);
    }

    public async Task<LoanRenewalResult> RenewLoanAsync(long loanId, CancellationToken cancellationToken)
    {
        using var message = CreateRequest(HttpMethod.Post, $"api/library/loans/{loanId}/renew");
        message.Content = JsonContent.Create(new { });
        using var response = await _client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<LoanRenewalResult>(
                cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("API gia hạn trả về dữ liệu rỗng.");
    }

    public async Task<ApiUser?> GetUserAsync(string studentCode, CancellationToken cancellationToken)
    {
        using var message = CreateRequest(
            HttpMethod.Get,
            $"api/library/users/{Uri.EscapeDataString(studentCode)}");
        using var response = await _client.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiUser>(cancellationToken: cancellationToken);
    }

    public async Task<(bool Succeeded, string Message)> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        using var message = CreateRequest(HttpMethod.Post, "api/auth/change-password");
        message.Content = JsonContent.Create(new
        {
            request.CurrentPassword,
            request.NewPassword
        });
        using var response = await _client.SendAsync(message, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<ApiMessage>(cancellationToken: cancellationToken);
        if (response.IsSuccessStatusCode)
            return result is null
                ? throw new InvalidDataException("API đổi mật khẩu trả về dữ liệu rỗng.")
                : (true, result.Message);

        var problem = await ReadProblemAsync(response, cancellationToken);
        return (false, problem);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        var token = httpContextAccessor.HttpContext?.Session.GetString("AccessToken");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<string> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken: cancellationToken);
        return problem?.Detail
            ?? problem?.Title
            ?? $"Yêu cầu thất bại với mã HTTP {(int)response.StatusCode}.";
    }

    private static async Task<LibraryApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        new(response.StatusCode, await ReadProblemAsync(response, cancellationToken));

    public sealed class LibraryApiException(HttpStatusCode statusCode, string message)
        : Exception(message)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }

    private sealed record ApiProblem(string? Title, string? Detail);
}
