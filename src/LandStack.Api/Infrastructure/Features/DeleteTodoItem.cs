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
    public class DeleteTodoItemRequest : IRequest
    {
        public Guid ToDoItemId { get; init; }

        public static DeleteTodoItemRequest Make(Guid todoItemId)
        {
            return new DeleteTodoItemRequest { ToDoItemId = todoItemId };
        }
    }

    public class DeleteTodoItem : IRequestHandler<DeleteTodoItemRequest>
    {
        private readonly ILiteCollection<ToDoItem> _items;
        private readonly IDispatcher _dispatcher;

        public DeleteTodoItem(LiteDbProvider dbProvider, IDispatcher dispatcher)
        {
            _items = dbProvider.Database.GetCollection<ToDoItem>(nameof(ToDoItem));
            _dispatcher = dispatcher;
        }

        public async Task Handle(DeleteTodoItemRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _items.Delete(request.ToDoItemId);
            await _dispatcher.Notify(TodoNotification.Make(NotificationAction.ItemDeleted, request.ToDoItemId), cancellationToken);
        }
    }
}