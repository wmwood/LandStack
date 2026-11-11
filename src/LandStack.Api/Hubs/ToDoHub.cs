using System;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Dto;
using Microsoft.AspNetCore.SignalR;
using LandStack.Api.Infrastructure.Dispatcher;
using LandStack.Api.Infrastructure.Features;
using System.Collections.Generic;

namespace LandStack.Api.Hubs
{
    public class ToDoHub : Hub
    {
        private readonly IDispatcher _dispatcher;

        public ToDoHub(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public async Task<List<ToDoItemDto>> GetAllItems()
        {
            return await _dispatcher.Execute(GetAllItemsRequest.Make(), Context.ConnectionAborted);
        }

        public async Task Create(CreateTodoItemDto dto)
        {
            await _dispatcher.Execute(CreateTodoItemRequest.Make(dto.Description), Context.ConnectionAborted);
        }

        public async Task ToggleComplete(Guid todoItemId)
        {
            await _dispatcher.Execute(ToggleCompleteTodoItemRequest.Make(todoItemId), Context.ConnectionAborted);
        }

        public async Task Delete(Guid todoItemId)
        {
            await _dispatcher.Execute(DeleteTodoItemRequest.Make(todoItemId), Context.ConnectionAborted);
        }
    }
}