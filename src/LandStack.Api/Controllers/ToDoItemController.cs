using System;
using System.Threading;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Extensions;
using LandStack.Api.Infrastructure.Features;
using LandStack.Api.Infrastructure.Dispatcher;
using Microsoft.AspNetCore.Mvc;

namespace LandStack.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ToDoItemController : ControllerBase
    {
        private readonly IDispatcher _dispatcher;

        public ToDoItemController(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            return Ok(await _dispatcher.Execute(GetAllItemsRequest.Make(), cancellationToken));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateTodoItemDto dto, CancellationToken cancellationToken)
        {
            var createdItem = await _dispatcher.Execute(CreateTodoItemRequest.Make(dto.Description), cancellationToken);
            return base.Created(this.GetResourceUrl(createdItem.ToDoItemId), createdItem);
        }

        [HttpPost("toggleComplete/{toDoItemId}")]
        public async Task<IActionResult> ToggleComplete(Guid todoItemId, CancellationToken cancellationToken)
        {
            await _dispatcher.Execute(ToggleCompleteTodoItemRequest.Make(todoItemId), cancellationToken);
            return Ok();
        }

        [HttpDelete]
        [Route("{toDoItemId}")]
        public async Task<IActionResult> Delete(Guid todoItemId, CancellationToken cancellationToken)
        {
            await _dispatcher.Execute(DeleteTodoItemRequest.Make(todoItemId), cancellationToken);
            return Ok();
        }
    }
}
