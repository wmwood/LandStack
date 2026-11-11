using System;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LandStack.Api.Hubs;

public sealed class TodoHubFilter(ILogger<TodoHubFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(context);
        }
        catch (TodoValidationException exception)
        {
            throw new HubException(exception.Message);
        }
        catch (TodoItemNotFoundException exception)
        {
            throw new HubException(exception.Message);
        }
        catch (OperationCanceledException) when (context.Context.ConnectionAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled to-do hub invocation failure in {Method}.", context.HubMethodName);
            throw new HubException("An unexpected error occurred.");
        }
    }
}
