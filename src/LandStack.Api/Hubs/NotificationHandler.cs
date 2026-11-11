using System;
using System.Threading;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Dispatcher;
using LandStack.Api.Infrastructure.Extensions;
using LandStack.Api.Infrastructure.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LandStack.Api.Hubs;

public sealed class NotificationHandler(IHubContext<ToDoHub> hub, ILogger<NotificationHandler> logger)
    : INotificationHandler<TodoNotification>
{
    public async Task Handle(TodoNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.All.SendAsync(notification.Action.ToCamelCaseString(), notification.Data, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to broadcast to-do event {Action} after persistence. The write was not rolled back.", notification.Action);
            throw;
        }
    }
}
