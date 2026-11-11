using System.Diagnostics.CodeAnalysis;
using System;

namespace LandStack.Api.Infrastructure.Dto
{
    public class ToDoItemDto
    {
        public Guid ToDoItemId { get; init; }
        // Attributes preserve nullable analysis without MTT making the wire property optional.
        [AllowNull, MaybeNull]
        public string Description { get; init; } = string.Empty;
        public bool IsCompleted { get; init; }
        public DateTime CreatedDate { get; init; }
    }
}