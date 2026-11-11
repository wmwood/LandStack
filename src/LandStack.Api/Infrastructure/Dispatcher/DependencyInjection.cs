using LandStack.Api.Hubs;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Features;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace LandStack.Api.Infrastructure.Dispatcher;

public static class DependencyInjection
{
    public static IServiceCollection AddTodoDispatcher(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IRequestHandler<GetAllItemsRequest, List<ToDoItemDto>>, GetAllItems>();
        services.AddScoped<IRequestHandler<CreateTodoItemRequest, ToDoItemDto>, CreateTodoItem>();
        services.AddScoped<IRequestHandler<ToggleCompleteTodoItemRequest>, ToggleCompleteTodoItem>();
        services.AddScoped<IRequestHandler<DeleteTodoItemRequest>, DeleteTodoItem>();
        services.AddScoped<INotificationHandler<TodoNotification>, NotificationHandler>();
        return services;
    }
}
