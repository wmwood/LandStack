using System;

namespace LandStack.Api.Infrastructure.Data.Models
{
    public record ToDoItem
    {
        public ToDoItem() { }

        public ToDoItem(string description, DateTime now)
        {
            ToDoItemId = Guid.NewGuid();
            Description = description;
            CreatedDate = now;
        }

        public Guid ToDoItemId { get; init; }
        public string Description { get; init; }
        public bool IsCompleted { get; init; }
        public DateTime CreatedDate { get; init; }
    }
}