using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Extensions;
using LandStack.Api.Infrastructure.Features;
using LandStack.Api.Infrastructure.Dispatcher;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LandStack.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public class ToDoItemController : ControllerBase
    {
        private readonly IDispatcher _dispatcher;

        public ToDoItemController(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        [HttpGet]
        [EndpointSummary("List to-do items")]
        [EndpointDescription("Lists items in creation-date order.")]
        [ProducesResponseType<List<ToDoItemDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            return Ok(await _dispatcher.Execute(GetAllItemsRequest.Make(), cancellationToken));
        }

        [HttpPost]
        [EndpointSummary("Create a to-do item")]
        [EndpointDescription("Description is required and must contain non-whitespace text. Valid text is preserved as supplied.")]
        [ProducesResponseType<ToDoItemDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        public async Task<IActionResult> Post([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateTodoItemDto? dto, CancellationToken cancellationToken)
        {
            var createdItem = await _dispatcher.Execute(CreateTodoItemRequest.Make(dto?.Description), cancellationToken);
            return base.Created(this.GetResourceUrl(createdItem.ToDoItemId), createdItem);
        }

        [HttpPost("toggleComplete/{toDoItemId}")]
        [EndpointSummary("Toggle item completion")]
        [EndpointDescription("Toggles an existing item. A missing item returns 404.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
        public async Task<IActionResult> ToggleComplete(Guid todoItemId, CancellationToken cancellationToken)
        {
            await _dispatcher.Execute(ToggleCompleteTodoItemRequest.Make(todoItemId), cancellationToken);
            return Ok();
        }

        [HttpDelete]
        [EndpointSummary("Delete a to-do item")]
        [EndpointDescription("Deletes an item. An absent item also succeeds.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [Route("{toDoItemId}")]
        public async Task<IActionResult> Delete(Guid todoItemId, CancellationToken cancellationToken)
        {
            await _dispatcher.Execute(DeleteTodoItemRequest.Make(todoItemId), cancellationToken);
            return Ok();
        }
    }
}
