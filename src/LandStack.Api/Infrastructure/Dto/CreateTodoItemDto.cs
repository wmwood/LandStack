using LandStack.Api.Infrastructure.Features;
using MediatR;

namespace LandStack.Api.Infrastructure.Dto
{
    public class CreateTodoItemDto
    {
        public string Description { get; init; }
    }
}