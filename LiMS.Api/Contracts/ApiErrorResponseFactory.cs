using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LiMS.Api.Contracts;

internal static class ApiErrorResponseFactory
{
    internal static ApiResponse<object?> CreateValidationResponse(
        ModelStateDictionary modelState,
        HttpContext httpContext)
    {
        var errors = modelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(_ => "Giá trị không hợp lệ hoặc đang bị thiếu.")
                    .ToArray());

        return ApiResponse<object?>.Failure(
            "VALIDATION_ERROR",
            "Dữ liệu gửi lên không hợp lệ. Vui lòng kiểm tra các trường được báo lỗi.",
            errors) with
        {
            TraceId = httpContext.TraceIdentifier
        };
    }

    internal static ApiResponse<object?> CreateStatusCodeResponse(
        HttpContext httpContext)
    {
        var (code, message) = httpContext.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized =>
                ("UNAUTHORIZED", "Vui lòng đăng nhập để tiếp tục."),
            StatusCodes.Status403Forbidden =>
                ("FORBIDDEN", "Tài khoản không có quyền thực hiện thao tác này."),
            StatusCodes.Status404NotFound =>
                ("NOT_FOUND", "Không tìm thấy tài nguyên được yêu cầu."),
            StatusCodes.Status405MethodNotAllowed =>
                ("METHOD_NOT_ALLOWED", "Phương thức yêu cầu không được hỗ trợ."),
            _ =>
                ("REQUEST_FAILED", "Yêu cầu không thể hoàn tất.")
        };

        return ApiResponse<object?>.Failure(code, message) with
        {
            TraceId = httpContext.TraceIdentifier
        };
    }
}