using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LiMS.Api.Contracts;
using LiMS.Api.Services;

namespace LiMS.Api.Middleware;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return false;

        var (statusCode, code, message) = exception switch
        {
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                exception.Message),
            ArgumentException => (
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                exception.Message),
            BusinessRuleException => (
                StatusCodes.Status409Conflict,
                "BUSINESS_RULE_VIOLATION",
                exception.Message),
            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                "FORBIDDEN",
                "Tài khoản không có quyền thực hiện thao tác này."),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "CONCURRENCY_CONFLICT",
                "Dữ liệu đã thay đổi trong lúc xử lý. Vui lòng tải lại và thử lại."),
            DbUpdateException updateException when IsUniqueConstraintViolation(updateException) => (
                StatusCodes.Status409Conflict,
                "DUPLICATE_RECORD",
                "Thông tin bị trùng với một bản ghi đã tồn tại."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled API exception returned {StatusCode}.", statusCode);
        else
            logger.LogWarning(exception, "Handled API exception {ErrorCode} with status {StatusCode}.", code, statusCode);
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            ApiResponse<object?>.Failure(code, message) with
            {
                TraceId = httpContext.TraceIdentifier
            },
            cancellationToken);
        return true;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        FindSqlException(exception)?.Number is 2601 or 2627;

    private static SqlException? FindSqlException(Exception exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException)
                return sqlException;
        }

        return null;
    }
}
