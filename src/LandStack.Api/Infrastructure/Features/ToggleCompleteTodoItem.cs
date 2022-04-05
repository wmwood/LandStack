using System;
using System.Threading;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Data.Models;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Services;
using LiteDB;
using MediatR;

namespace LandStack.Api.Infrastructure.Features
{
    public record ToggleCompleteTodoItemRequest : IRequest
    {
        public Guid ToDoItemId { get; init; }

        public static ToggleCompleteTodoItemRequest Make(Guid todoItemId)
        {
            return new ToggleCompleteTodoItemRequest { ToDoItemId = todoItemId };
        }
    }

    public class ToggleCompleteTodoItem : IRequestHandler<ToggleCompleteTodoItemRequest>
    {
        private readonly ILiteCollection<ToDoItem> _items;
        private readonly IMediator _mediator;

        public ToggleCompleteTodoItem(LiteDbProvider dbProvider, IMediator mediator)
        {
            _items = dbProvider.Database.GetCollection<ToDoItem>(nameof(ToDoItem));
            _mediator = mediator;
        }

        public Task<Unit> Handle(ToggleCompleteTodoItemRequest request, CancellationToken cancellationToken)
        {
            var item = _items.FindById(request.ToDoItemId);

            var updatedItem = item with
            {
                IsCompleted = !item.IsCompleted
            };

            _items.Update(updatedItem);

            _mediator.Publish(TodoNotification.Make(NotificationAction.ItemUpdated, updatedItem));

            return Unit.Task;
        }
    }
}