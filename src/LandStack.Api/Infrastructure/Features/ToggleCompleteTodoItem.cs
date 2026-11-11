using System;
using System.Threading;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Data.Models;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Services;
using LiteDB;
using LandStack.Api.Infrastructure.Dispatcher;

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
        private readonly IDispatcher _dispatcher;

        public ToggleCompleteTodoItem(LiteDbProvider dbProvider, IDispatcher dispatcher)
        {
            _items = dbProvider.Database.GetCollection<ToDoItem>(nameof(ToDoItem));
            _dispatcher = dispatcher;
        }

        public async Task Handle(ToggleCompleteTodoItemRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = _items.FindById(request.ToDoItemId) ?? throw new TodoItemNotFoundException();

            var updatedItem = item with
            {
                IsCompleted = !item.IsCompleted
            };

            _items.Update(updatedItem);

            await _dispatcher.Notify(TodoNotification.Make(NotificationAction.ItemUpdated, updatedItem), cancellationToken);

        }
    }
}