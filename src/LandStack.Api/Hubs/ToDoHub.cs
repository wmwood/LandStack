using System;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;

namespace LandStack.Api.Hubs
{
    public class ToDoHub : Hub
    {
        private readonly ToDoItemService _toDoItemService;

        public ToDoHub(ToDoItemService toDoItemService)
        {
            _toDoItemService = toDoItemService;
        }

        public override async Task OnConnectedAsync()
        {
            await this.Clients.Client(Context.ConnectionId).SendAsync("LoadTodoItems", await _toDoItemService.GetItems());
            await base.OnConnectedAsync();
        }

        public async Task Add(CreateTodoDto dto)
        {
            await _toDoItemService.Create(dto.Description);
        }

        public async Task Update(ToDoItemDto dto)
        {
            await _toDoItemService.Update(dto);
        }

        public async Task Delete(Guid todoItemId)
        {
            await _toDoItemService.Delete(todoItemId);
        }
    }
}