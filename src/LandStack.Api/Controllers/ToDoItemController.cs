using System;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Extensions;
using LandStack.Api.Infrastructure.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LandStack.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ToDoItemController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ToDoItemController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return Ok(await _mediator.Send(GetAllItemsRequest.Make()));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateTodoItemDto dto)
        {
            var createdItem = await _mediator.Send(CreateTodoItemRequest.Make(dto.Description));
            return base.Created(this.GetResourceUrl(createdItem.ToDoItemId), createdItem);
        }

        [HttpPost("toggleComplete/{toDoItemId}")]
        public async Task<IActionResult> ToggleComplete(Guid todoItemId)
        {
            await _mediator.Send(ToggleCompleteTodoItemRequest.Make(todoItemId));
            return Ok();
        }

        [HttpDelete]
        [Route("{toDoItemId}")]
        public async Task<IActionResult> Delete(Guid todoItemId)
        {
            await _mediator.Send(DeleteTodoItemRequest.Make(todoItemId));
            return Ok();
        }
    }
}
