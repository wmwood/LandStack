using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LandStack.Api.Hubs;
using LandStack.Api.Infrastructure.Dto;
using Microsoft.AspNetCore.SignalR;

namespace LandStack.Api.Infrastructure.Services
{
    public class ToDoItemService
    {
        private readonly IToDoItemGateway _toDoItemGateway;
        private readonly IHubContext<ToDoHub> _hub;

        public ToDoItemService(IToDoItemGateway toDoItemGateway, IHubContext<ToDoHub> hub)
        {
            _toDoItemGateway = toDoItemGateway;
            _hub = hub;
        }

        public async Task<List<ToDoItemDto>> GetItems()
        {
            return await _toDoItemGateway.GetItems();
        }

        public async Task<ToDoItemDto> GetItem(Guid toDoItemId)
        {
            return await _toDoItemGateway.GetItem(toDoItemId);
        }

        public async Task<ToDoItemDto> Create(string description)
        {
            var createdItem = await _toDoItemGateway.CreateItem(description);
            await _hub.Clients.All.SendAsync("AddTodoItem", createdItem);
            return createdItem;
        }

        public async Task Update(ToDoItemDto updated)
        {
            await _toDoItemGateway.Update(updated);
            await _hub.Clients.All.SendAsync("UpdateTodoItem", updated);
        }

        public async Task Delete(Guid toDoItemId)
        {
            await _toDoItemGateway.Delete(toDoItemId);
            await _hub.Clients.All.SendAsync("DeleteTodoItem", toDoItemId);
        }
    }
}