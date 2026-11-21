using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using LiMS.Api.Controllers;
using LiMS.Api.Contracts;
using LiMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;

namespace LiMS.Tests;

public sealed class AuthorizationAndClientTests
{
    [Fact]
    public void Controllers_declare_expected_role_policies()
    {
        Assert.Equal("Admin,Librarian", typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>()?.Roles);
        Assert.Equal("Borrower", typeof(ReaderController).GetCustomAttribute<AuthorizeAttribute>()?.Roles);
        Assert.NotNull(typeof(AuthController).GetMethod(nameof(AuthController.Login))
            ?.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(typeof(LibraryController).GetMethod(nameof(LibraryController.Books))
            ?.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(typeof(LibraryController).GetMethod(nameof(LibraryController.GetUser))
            ?.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Web_client_escapes_reader_code_when_loading_loans()
    {
        Uri? requestedUri = null;
        var handler = new RecordingHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<object>())
            });
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("LiMS.Api")).Returns(client);
        var service = new LibraryApiClient(factory.Object, new HttpContextAccessor());

        await service.GetLoansAsync("reader/a", CancellationToken.None);

        Assert.NotNull(requestedUri);
        Assert.EndsWith("api/library/loans/reader%2Fa", requestedUri!.AbsoluteUri);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request, cancellationToken);
    }
}
