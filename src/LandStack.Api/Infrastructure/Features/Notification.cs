using LandStack.Api.Infrastructure.Dto;
using MediatR;

namespace LandStack.Api.Infrastructure.Features
{
    public record TodoNotification : INotification
    {
        public NotificationAction Action { get; init; }
        public object Data { get; init; }

        public static TodoNotification Make(NotificationAction action, object data)
        {
            return new TodoNotification
            {
                Action = action,
                Data = data
            };
        }
    }
}