using System.Net;
using System.Net.Http.Json;
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
}
