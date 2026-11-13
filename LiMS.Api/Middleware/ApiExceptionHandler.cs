using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LiMS.Api.Services;

namespace LiMS.Api.Middleware;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "Not Found",
                exception.Message),
            ArgumentException => (
                StatusCodes.Status400BadRequest,
                "Invalid Request",
                exception.Message),
            BusinessRuleException => (
                StatusCodes.Status409Conflict,
                "Conflict",
                exception.Message),
            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "The current account is not permitted to perform this operation."),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrency Conflict",
                "The record changed during this operation. Reload it and try again."),
            DbUpdateException updateException when IsUniqueConstraintViolation(updateException) => (
                StatusCodes.Status409Conflict,
                "Duplicate Record",
                "A record with the same unique value already exists."),
            _ => (0, (string?)null, null)
        };

        if (statusCode == 0)
            return false;

        logger.LogWarning(exception, "Handled API exception with status {StatusCode}.", statusCode);
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        }, cancellationToken);
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
