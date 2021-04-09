using System;

namespace LandStack.Api.Infrastructure.Dto
{
    public class ToDoItemDto
    {
        public Guid ToDoItemId { get; init; }
        public string Description { get; init; }
        public bool IsCompleted { get; init; }
        public DateTime CreatedDate { get; init; }
    }
}