using System.Net;
using System.Net.Http.Json;
using LiMS.Admin.Services;

namespace LiMS.Tests;

public sealed class AdminApiClientTraceIdTests
{
    [Fact]
    public async Task CreateExceptionAsync_reads_trace_id_from_API_error()
    {
        const string traceId = "admin-trace-123";

        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = JsonContent.Create(new
            {
                code = "INTERNAL_SERVER_ERROR",
                message = "Đã xảy ra lỗi hệ thống.",
                traceId
            })
        };

        var exception = await AdminApiClient.CreateExceptionAsync(
            response,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(traceId, exception.TraceId);
        Assert.Contains(traceId, exception.Message);
    }

    [Fact]
    public async Task CreateExceptionAsync_handles_error_without_trace_id()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new
            {
                message = "Dữ liệu không hợp lệ."
            })
        };

        var exception = await AdminApiClient.CreateExceptionAsync(
            response,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Null(exception.TraceId);
        Assert.Contains("Dữ liệu không hợp lệ.", exception.Message);
        Assert.DoesNotContain("TraceId", exception.Message);
    }

    [Fact]
    public async Task CreateExceptionAsync_preserves_forbidden_status_code()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = JsonContent.Create(new
            {
                message = "Bạn không có quyền thực hiện thao tác.",
                traceId = "forbidden-trace-456"
            })
        };

        var exception = await AdminApiClient.CreateExceptionAsync(
            response,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal("forbidden-trace-456", exception.TraceId);
    }
}