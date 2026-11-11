#nullable enable
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace LandStack.Api.Infrastructure.Dispatcher;

public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    public Task<TResponse> Execute<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        return Invoke<Task<TResponse>>(handlerType, GetSingleHandler(handlerType), request, cancellationToken);
    }

    public Task Execute<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var handlerType = typeof(IRequestHandler<>).MakeGenericType(request.GetType());
        return Invoke<Task>(handlerType, GetSingleHandler(handlerType), request, cancellationToken);
    }

    public Task Notify<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notification.GetType());
        return Task.WhenAll(services.GetServices(handlerType).Select(InvokeNotification).ToArray());

        // Capture synchronous faults as tasks too, so every handler is invoked and awaited.
        async Task InvokeNotification(object? handler)
        {
            await Invoke<Task>(handlerType, handler!, notification, cancellationToken);
        }
    }

    private object GetSingleHandler(Type handlerType)
    {
        var handlers = services.GetServices(handlerType).ToArray();
        if (handlers.Length != 1 || handlers[0] is null)
            throw new InvalidOperationException($"Expected exactly one {handlerType} registration; found {handlers.Length}.");
        return handlers[0]!;
    }

    private static TTask Invoke<TTask>(Type handlerType, object handler, object request, CancellationToken cancellationToken)
        where TTask : Task
    {
        try
        {
            // Invoking the interface supports internal handlers and explicit implementations.
            return (TTask)handlerType.GetMethod("Handle")!.Invoke(handler, [request, cancellationToken])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
