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
        private readonly IMediator _mediator;

        public DeleteTodoItem(LiteDbProvider dbProvider, IMediator mediator)
        {
            _items = dbProvider.Database.GetCollection<ToDoItem>(nameof(ToDoItem));
            _mediator = mediator;
        }

        public Task<Unit> Handle(DeleteTodoItemRequest request, CancellationToken cancellationToken)
        {
            _items.Delete(request.ToDoItemId);
            _mediator.Publish(TodoNotification.Make(NotificationAction.ItemDeleted, request.ToDoItemId));
            return Unit.Task;
        }
    }
}