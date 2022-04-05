using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Data.Models;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Services;
using LiteDB;
using MediatR;

namespace LandStack.Api.Infrastructure.Features
{
    public record GetAllItemsRequest : IRequest<List<ToDoItemDto>>
    {
        public static GetAllItemsRequest Make() => new GetAllItemsRequest();
    }

    public class GetAllItems : IRequestHandler<GetAllItemsRequest, List<ToDoItemDto>>
    {
        private readonly ILiteCollection<ToDoItem> _items;

        public GetAllItems(LiteDbProvider dbProvider)
        {
            _items = dbProvider.Database.GetCollection<ToDoItem>(nameof(ToDoItem));
        }

        public Task<List<ToDoItemDto>> Handle(GetAllItemsRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_items.Query().OrderBy(i => i.CreatedDate).Select(item => new ToDoItemDto
            {
                ToDoItemId = item.ToDoItemId,
                Description = item.Description,
                IsCompleted = item.IsCompleted,
                CreatedDate = item.CreatedDate
            }).ToList());
        }
    }
}