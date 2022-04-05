using System;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Dto;
using Microsoft.AspNetCore.SignalR;
using MediatR;
using LandStack.Api.Infrastructure.Features;
using System.Collections.Generic;

namespace LandStack.Api.Hubs
{
    public class ToDoHub : Hub
    {
        private readonly IMediator _mediator;

        public ToDoHub(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<List<ToDoItemDto>> GetAllItems()
        {
            return await _mediator.Send(GetAllItemsRequest.Make());
        }

        public async Task Create(CreateTodoItemDto dto)
        {
            await _mediator.Send(CreateTodoItemRequest.Make(dto.Description));
        }

        public async Task ToggleComplete(Guid todoItemId)
        {
            await _mediator.Send(ToggleCompleteTodoItemRequest.Make(todoItemId));
        }

        public async Task Delete(Guid todoItemId)
        {
            await _mediator.Send(DeleteTodoItemRequest.Make(todoItemId));
        }
    }
}