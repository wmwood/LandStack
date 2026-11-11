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
    public record CreateTodoItemRequest : IRequest<ToDoItemDto>
    {
        public string? Description { get; init; }

        public static CreateTodoItemRequest Make(string? description)
        {
            return new CreateTodoItemRequest
            {
                Description = description
            };
        }
    }

    public class CreateTodoItem : IRequestHandler<CreateTodoItemRequest, ToDoItemDto>
    {
        private readonly TimeProvider _timeProvider;
        private readonly ILiteCollection<ToDoItem> _items;
        private readonly IDispatcher _dispatcher;

        public CreateTodoItem(LiteDbProvider dbProvider, TimeProvider timeProvider, IDispatcher dispatcher)
        {
            _items = dbProvider.Database.GetCollection<ToDoItem>(nameof(ToDoItem));
            _timeProvider = timeProvider;
            _dispatcher = dispatcher;
        }

        public async Task<ToDoItemDto> Handle(CreateTodoItemRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new TodoValidationException();
            var item = new ToDoItem(request.Description, _timeProvider.GetUtcNow().UtcDateTime);

            _items.Insert(item);

            var createdItem = new ToDoItemDto
            {
                ToDoItemId = item.ToDoItemId,
                Description = item.Description,
                IsCompleted = item.IsCompleted,
                CreatedDate = item.CreatedDate
            };

            await _dispatcher.Notify(TodoNotification.Make(NotificationAction.ItemCreated, createdItem), cancellationToken);

            return createdItem;
        }
    }
}