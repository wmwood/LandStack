using System;
using System.Threading.Tasks;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Extensions;
using LandStack.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace LandStack.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ToDoItemController : ControllerBase
    {
        private readonly ToDoItemService _toDoItemService;

        public ToDoItemController(ToDoItemService toDoItemService)
        {
            _toDoItemService = toDoItemService;
        }

        [HttpGet]
        [Route("{toDoItemId?}")]
        public async Task<IActionResult> Get(Guid? toDoItemId)
        {
            if (toDoItemId.HasValue)
                return Ok(await _toDoItemService.GetItem(toDoItemId.Value));

            return Ok(await _toDoItemService.GetItems());
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateTodoDto dto)
        {
            var createdItem = await _toDoItemService.Create(dto.Description);
            return base.Created(this.GetResourceUrl(createdItem.ToDoItemId), createdItem);
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] ToDoItemDto item)
        {
            await _toDoItemService.Update(item);
            return Ok();
        }

        [HttpDelete]
        [Route("{toDoItemId}")]
        public async Task<IActionResult> Delete(Guid toDoItemId)
        {
            await _toDoItemService.Delete(toDoItemId);
            return Ok();
        }
    }
}
