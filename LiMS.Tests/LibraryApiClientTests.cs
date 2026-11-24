using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using LiMS.Api.Contracts;
using LiMS.Web.Models;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace LiMS.Tests;

public sealed class LibraryApiClientTests
{
    [Fact]
    public async Task LoginAsync_returns_null_for_unauthorized_response()
    {
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var service = CreateService(client);

        var user = await service.LoginAsync(
            new LiMS.Web.Models.LoginRequest("unknown", "incorrect"),
            CancellationToken.None);

        Assert.Null(user);
    }

    [Fact]
    public async Task LoginAsync_deserializes_authenticated_user_response()
    {
        var expected = new ApiUser(8, "CARD-8", "Reader", "reader@example.test", "Borrower", "access-token");
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected)
        }));
        var service = CreateService(client);

        var user = await service.LoginAsync(
            new LiMS.Web.Models.LoginRequest("reader", "password"),
            CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal(expected.Id, user.Id);
        Assert.Equal(expected.AccessToken, user.AccessToken);
    }

    [Fact]
    public async Task GetBooksAsync_deserializes_existing_public_catalog_contract()
    {
        var response = new[]
        {
            new BookResponse(1, "Catalog title", "Writer", "123", "History", null, 2020, 2, 1, 1, null, null, null)
        };
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response)
        }));
        var service = CreateService(client);

        var books = await service.GetBooksAsync(CancellationToken.None);

        var book = Assert.Single(books);
        Assert.Equal(1, book.Id);
        Assert.Equal("Catalog title", book.Title);
        Assert.Equal(1, book.AvailableCopies);
    }

    [Fact]
    public async Task GetBooksPageAsync_encodes_filters_and_deserializes_page_metadata()
    {
        var expected = new ApiPagedResponse<ApiBook>(
            [new ApiBook(3, "Catalog title", "Writer", null, "History", null, 2020, 2, 1, 1)],
            31,
            2,
            24);
        string? requestPath = null;
        using var client = CreateClient((request, _) =>
        {
            requestPath = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            });
        });
        var service = CreateService(client);

        var result = await service.GetBooksPageAsync(
            "C# 12", ".NET", "available", "title", 2, 24, CancellationToken.None);

        Assert.NotNull(requestPath);
        Assert.Contains("/api/library/books/paged?", requestPath);
        Assert.Contains("search=C%23%2012", requestPath);
        Assert.Contains("category=.NET", requestPath);
        Assert.Equal(31, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal("Catalog title", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task CreateReservationAsync_preserves_API_business_error_message()
    {
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(ApiResponse<object?>.Failure(
                "BUSINESS_RULE_VIOLATION",
                "Sách đang có bản sao sẵn sàng."))
        }));
        var service = CreateService(client);

        var error = await Assert.ThrowsAsync<LibraryApiClient.LibraryApiException>(
            () => service.CreateReservationAsync(3, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
        Assert.Equal("Sách đang có bản sao sẵn sàng.", error.Message);
    }

    [Fact]
    public async Task CreateReservationAsync_includes_API_trace_id_in_the_error_notice()
    {
        const string traceId = "request-trace-123";
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = JsonContent.Create(ApiResponse<object?>.Failure(
                "INTERNAL_SERVER_ERROR",
                "Đã xảy ra lỗi hệ thống.") with { TraceId = traceId })
        }));
        var service = CreateService(client);

        var error = await Assert.ThrowsAsync<LibraryApiClient.LibraryApiException>(
            () => service.CreateReservationAsync(3, CancellationToken.None));

        Assert.Equal(traceId, error.TraceId);
        Assert.Contains(traceId, error.Message);
    }

    [Fact]
    public async Task GetUserAsync_returns_null_when_account_is_not_found()
    {
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var service = CreateService(client);

        var user = await service.GetUserAsync("missing-reader", CancellationToken.None);

        Assert.Null(user);
    }

    [Fact]
    public async Task GetNotificationsAsync_deserializes_notification_contract()
    {
        var expected = new[]
        {
            new ReaderNotificationResponse(
                12,
                "Ready",
                "Book is ready",
                "ReservationReady",
                DateTimeOffset.UtcNow,
                false)
        };
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected)
        }));
        var service = CreateService(client);

        var notifications = await service.GetNotificationsAsync(CancellationToken.None);

        var notification = Assert.Single(notifications);
        Assert.Equal(expected[0].Title, notification.Title);
        Assert.Equal(expected[0].Type, notification.Type);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task ChangePasswordAsync_returns_success_message_from_API()
    {
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new LiMS.Web.Models.ApiMessage("Password changed"))
        }));
        var service = CreateService(client);

        var result = await service.ChangePasswordAsync(
            new LiMS.Web.Models.ChangePasswordRequest("reader", "old-password", "new-password"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Password changed", result.Message);
    }

    [Fact]
    public async Task ChangePasswordAsync_returns_API_problem_without_throwing_for_rejected_password()
    {
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { code = "CURRENT_PASSWORD_INVALID", message = "Current password is wrong" })
        }));
        var service = CreateService(client);

        var result = await service.ChangePasswordAsync(
            new LiMS.Web.Models.ChangePasswordRequest("reader", "wrong-password", "new-password"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Current password is wrong", result.Message);
    }

    [Fact]
    public void GetAbsoluteBookCoverUrl_resolves_relative_api_path()
    {
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var service = CreateService(client);

        var absolute = service.GetAbsoluteBookCoverUrl("/api/library/books/12/cover");

        Assert.Equal("https://api.example/api/library/books/12/cover", absolute);
        Assert.Null(service.GetAbsoluteBookCoverUrl("  "));
    }

    [Fact]
    public async Task GetLoansAsync_sends_session_access_token_as_bearer_header()
    {
        AuthenticationHeaderValue? authorization = null;
        var handler = new StubHandler((request, _) =>
        {
            authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<ApiLoan>())
            });
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("LiMS.Api")).Returns(client);
        var session = new StubSession();
        session.Set("AccessToken", Encoding.UTF8.GetBytes("session-token"));
        var context = new DefaultHttpContext { Session = session };
        var service = new LibraryApiClient(factory.Object, new HttpContextAccessor { HttpContext = context });

        await service.GetLoansAsync("reader", CancellationToken.None);

        Assert.NotNull(authorization);
        Assert.Equal("Bearer", authorization.Scheme);
        Assert.Equal("session-token", authorization.Parameter);
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) =>
        new(new StubHandler(callback)) { BaseAddress = new Uri("https://api.example/") };

    private static LibraryApiClient CreateService(HttpClient client)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("LiMS.Api")).Returns(client);
        return new LibraryApiClient(factory.Object, new HttpContextAccessor());
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request, cancellationToken);
    }

    private sealed class StubSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public string Id => "test-session";
        public bool IsAvailable => true;
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value)
        {
            if (_values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = [];
            return false;
        }
    }
}
