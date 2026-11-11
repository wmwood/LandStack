using System;
using System.Threading;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Features;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LandStack.Api.Infrastructure.Services;

public sealed class TodoExceptionHandler(ILogger<TodoExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            TodoValidationException => (StatusCodes.Status400BadRequest, "Invalid to-do item", exception.Message),
            TodoItemNotFoundException => (StatusCodes.Status404NotFound, "To-do item not found", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", (string?)null)
        };
        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled to-do API failure.");
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
