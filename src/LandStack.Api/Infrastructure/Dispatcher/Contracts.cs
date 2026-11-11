#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace LandStack.Api.Infrastructure.Dispatcher;

public interface IRequest { }
public interface IRequest<out TResponse> { }
public interface INotification { }

public interface IRequestHandler<in TRequest> where TRequest : IRequest
{
    Task Handle(TRequest request, CancellationToken cancellationToken);
}

public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}

public interface IDispatcher
{
    Task<TResponse> Execute<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    Task Execute<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest;
    Task Notify<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
}
