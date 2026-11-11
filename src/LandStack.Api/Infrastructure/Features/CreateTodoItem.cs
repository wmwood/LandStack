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
        public string Description { get; init; }

        public static CreateTodoItemRequest Make(string description)
        {
            return new CreateTodoItemRequest
            {
                Description = description
            };
        }
    }

    public class CreateTodoItem : IRequestHandler<CreateTodoItemRequest, ToDoItemDto>
    {
        private readonly IClock _clock;
        private readonly ILiteCollection<ToDoItem> _items;
        private readonly IDispatcher _dispatcher;

        public CreateTodoItem(LiteDbProvider dbProvider, IClock clock, IDispatcher dispatcher)
        {
            _items = dbProvider.Database.GetCollection<ToDoItem>(nameof(ToDoItem));
            _clock = clock;
            _dispatcher = dispatcher;
        }

        public async Task<ToDoItemDto> Handle(CreateTodoItemRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new ToDoItem(request.Description, _clock.Now);

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