using System.Diagnostics.CodeAnalysis;
namespace LandStack.Api.Infrastructure.Dto
{
    public class CreateTodoItemDto
    {
        // Attributes preserve nullable analysis without MTT making the wire property optional.
        [AllowNull, MaybeNull]
        public string Description { get; init; } = string.Empty;
    }
}