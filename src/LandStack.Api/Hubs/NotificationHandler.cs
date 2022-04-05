using System.Threading;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Extensions;
using LandStack.Api.Infrastructure.Features;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace LandStack.Api.Hubs
{
    public class NotificationHandler : INotificationHandler<TodoNotification>
    {
        private readonly IMediator _mediator;
        private readonly IHubContext<ToDoHub> _hub;

        public NotificationHandler(IMediator mediator, IHubContext<ToDoHub> hub)
        {
            _mediator = mediator;
            _hub = hub;
        }

        public async Task Handle(TodoNotification notification, CancellationToken cancellationToken)
        {
            await _hub.Clients.All.SendAsync(notification.Action.ToCamelCaseString(), notification.Data);
        }
    }
}