using System.Text.Json;
using LiMS.Api.Middleware;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiMS.Tests;

public sealed class ApiExceptionHandlerTests
{
    [Theory]
    [InlineData(typeof(KeyNotFoundException), 404, "NOT_FOUND")]
    [InlineData(typeof(ArgumentException), 400, "INVALID_REQUEST")]
    [InlineData(typeof(BusinessRuleException), 409, "BUSINESS_RULE_VIOLATION")]
    public async Task TryHandleAsync_maps_known_exceptions_to_stable_responses(
        Type exceptionType,
        int expectedStatus,
        string expectedCode)
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var exception = (Exception)Activator.CreateInstance(exceptionType, "test failure")!;
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task TryHandleAsync_returns_generic_message_for_unexpected_exception()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("sensitive internal detail"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        var message = document.RootElement.GetProperty("message").GetString();
        Assert.DoesNotContain("sensitive internal detail", message);
        Assert.Equal(
            context.TraceIdentifier,
            document.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_does_not_handle_request_aborted_cancellation()
    {
        var context = new DefaultHttpContext();
        context.RequestAborted = new CancellationToken(canceled: true);
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new OperationCanceledException(),
            CancellationToken.None);

        Assert.False(handled);
    }
}
