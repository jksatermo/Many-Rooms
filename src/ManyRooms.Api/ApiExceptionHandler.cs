using ManyRooms.Application;
using ManyRooms.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ManyRooms.Api;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            BadHttpRequestException request => (request.StatusCode, "Invalid request", "Check the request body and Content-Type header."),
            AssignmentAccessException => (403, "Access denied", "You cannot respond to this assignment."),
            AssignmentNotFoundException => (404, "Assignment not found", "This assignment could not be found in this organization."),
            AssignmentConflictException => (409, "Response conflict", exception.Message),
            ArgumentException => (400, "Invalid response", exception.Message),
            _ => (500, "Something went wrong", "The request could not be completed. Please try again.")
        };
        if (status == 500) logger.LogError(exception, "Unhandled API failure for {Path}", context.Request.Path);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
