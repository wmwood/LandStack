using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Data.Models;
using LandStack.Api.Infrastructure.Dto;
using LiteDB;

namespace LandStack.Api.Infrastructure.Services
{
    public interface IToDoItemGateway
    {
        Task<ToDoItemDto> CreateItem(string description);
        Task Delete(Guid toDoItemId);
        Task<ToDoItemDto> GetItem(Guid toDoItemId);
        Task<List<ToDoItemDto>> GetItems();
        Task Update(ToDoItemDto updated);
    }

    public class ToDoItemLiteDbGateway : IToDoItemGateway
    {
        private readonly ILiteCollection<ToDoItem> _items;
        public IClock _clock { get; }

        public ToDoItemLiteDbGateway(LiteDbProvider dbProvider, IClock clock)
        {
            _clock = clock;
            _items = dbProvider.Database.GetCollection<ToDoItem>(nameof(ToDoItem));
        }

        public Task<List<ToDoItemDto>> GetItems()
        {
            return Task.FromResult(_items.Query().OrderBy(i => i.CreatedDate).Select(item => new ToDoItemDto
            {
                ToDoItemId = item.ToDoItemId,
                Description = item.Description,
                IsCompleted = item.IsCompleted,
                CreatedDate = item.CreatedDate
            }).ToList());
        }

        public Task<ToDoItemDto> GetItem(Guid toDoItemId)
        {
            var item = _items.FindById(toDoItemId);

            return Task.FromResult(new ToDoItemDto
            {
                ToDoItemId = item.ToDoItemId,
                Description = item.Description,
                IsCompleted = item.IsCompleted,
                CreatedDate = item.CreatedDate
            });
        }

        public async Task<ToDoItemDto> CreateItem(string description)
        {
            var item = new ToDoItem(description, _clock.Now);

            _items.Insert(item);

            return await GetItem(item.ToDoItemId);
        }

        public Task Update(ToDoItemDto updated)
        {
            var item = _items.FindById(updated.ToDoItemId);

            var updatedItem = item with
            {
                Description = updated.Description,
                IsCompleted = updated.IsCompleted
            };

            _items.Update(updatedItem);
            return Task.CompletedTask;
        }

        public Task Delete(Guid toDoItemId)
        {
            _items.Delete(toDoItemId);
            return Task.CompletedTask;
        }
    }
}