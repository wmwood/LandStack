using System;

namespace LandStack.Api.Infrastructure.Features;

public sealed class TodoValidationException() : Exception("Description must contain non-whitespace text.");
public sealed class TodoItemNotFoundException() : Exception("The to-do item was not found.");
