using System.Threading;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Data.Models;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Services;
using LiteDB;
using MediatR;

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
        private readonly IMediator _mediator;

        public CreateTodoItem(LiteDbProvider dbProvider, IClock clock, IMediator mediator)
        {
            _items = dbProvider.Database.GetCollection<ToDoItem>(nameof(ToDoItem));
            _clock = clock;
            _mediator = mediator;
        }

        public Task<ToDoItemDto> Handle(CreateTodoItemRequest request, CancellationToken cancellationToken)
        {
            var item = new ToDoItem(request.Description, _clock.Now);

            _items.Insert(item);

            var createdItem = new ToDoItemDto
            {
                ToDoItemId = item.ToDoItemId,
                Description = item.Description,
                IsCompleted = item.IsCompleted,
                CreatedDate = item.CreatedDate
            };

            _mediator.Publish(TodoNotification.Make(NotificationAction.ItemCreated, createdItem));

            return Task.FromResult(createdItem);
        }
    }
}