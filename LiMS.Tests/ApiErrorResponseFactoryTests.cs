using LiMS.Api.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LiMS.Tests;

public sealed class ApiErrorResponseFactoryTests
{
    [Fact]
    public void CreateValidationResponse_includes_errors_and_trace_id()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Title", "Invalid title");

        var context = new DefaultHttpContext
        {
            TraceIdentifier = "validation-trace-id"
        };

        var response = ApiErrorResponseFactory.CreateValidationResponse(
            modelState,
            context);

        Assert.False(response.Success);
        Assert.Equal("VALIDATION_ERROR", response.Code);
        Assert.Equal(
            "Dữ liệu gửi lên không hợp lệ. Vui lòng kiểm tra các trường được báo lỗi.",
            response.Message);
        Assert.Equal("validation-trace-id", response.TraceId);

        Assert.NotNull(response.Errors);
        Assert.Contains("Title", response.Errors.Keys);
        Assert.Equal(
            "Giá trị không hợp lệ hoặc đang bị thiếu.",
            Assert.Single(response.Errors["Title"]));
    }

    [Theory]
    [InlineData(401, "UNAUTHORIZED", "Vui lòng đăng nhập để tiếp tục.")]
    [InlineData(403, "FORBIDDEN", "Tài khoản không có quyền thực hiện thao tác này.")]
    [InlineData(404, "NOT_FOUND", "Không tìm thấy tài nguyên được yêu cầu.")]
    [InlineData(405, "METHOD_NOT_ALLOWED", "Phương thức yêu cầu không được hỗ trợ.")]
    [InlineData(418, "REQUEST_FAILED", "Yêu cầu không thể hoàn tất.")]
    public void CreateStatusCodeResponse_maps_status_and_includes_trace_id(
        int statusCode,
        string expectedCode,
        string expectedMessage)
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "status-trace-id"
        };

        context.Response.StatusCode = statusCode;

        var response = ApiErrorResponseFactory.CreateStatusCodeResponse(context);

        Assert.False(response.Success);
        Assert.Equal(expectedCode, response.Code);
        Assert.Equal(expectedMessage, response.Message);
        Assert.Equal("status-trace-id", response.TraceId);
    }
}