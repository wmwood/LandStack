using System.Security.Claims;
using LandStack.Api.Controllers;
using LandStack.Api.Hubs;
using LandStack.Api.Infrastructure.Data.Models;
using LandStack.Api.Infrastructure.Dispatcher;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Features;
using LandStack.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LandStack.Api.Tests;

public sealed class DispatchIntegrationTests
{
    [Fact]
    public async Task Controller_and_hub_share_handlers_preserve_success_results_and_forward_transport_tokens()
    {
        using var database = new IsolatedDatabase();
        using var services = database.OpenServices(new TestTimeProvider { UtcNow = DateTime.UtcNow });
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var token = TestContext.Current.CancellationToken;
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 5000);
        context.Request.Path = "/todoItem";
        var controller = new ToDoItemController(dispatcher)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
        using var hub = new ToDoHub(dispatcher) { Context = new TestHubCallerContext(token) };

        var result = Assert.IsType<CreatedResult>(await controller.Post(new CreateTodoItemDto { Description = "Created through REST" }, token));
        var created = Assert.IsType<ToDoItemDto>(result.Value);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal($"http://localhost:5000/todoItem/{created.ToDoItemId}", result.Location);
        Assert.Equal(created.ToDoItemId, Assert.Single(await hub.GetAllItems()).ToDoItemId);
        await hub.ToggleComplete(created.ToDoItemId);
        var listed = Assert.IsType<List<ToDoItemDto>>(Assert.IsType<OkObjectResult>(await controller.Get(token)).Value);
        Assert.True(Assert.Single(listed).IsCompleted);
        await hub.Create(new CreateTodoItemDto { Description = "Created through hub" });
        var hubCreated = (await hub.GetAllItems()).Single(i => i.ToDoItemId != created.ToDoItemId);
        Assert.IsType<OkResult>(await controller.ToggleComplete(hubCreated.ToDoItemId, token));
        await hub.Delete(created.ToDoItemId);
        Assert.IsType<OkResult>(await controller.Delete(hubCreated.ToDoItemId, token));
        Assert.Empty(await hub.GetAllItems());
        var events = services.GetRequiredService<RecordedNotifications>();
        Assert.Equal(new[] { NotificationAction.ItemCreated, NotificationAction.ItemUpdated, NotificationAction.ItemCreated,
            NotificationAction.ItemUpdated, NotificationAction.ItemDeleted, NotificationAction.ItemDeleted }, events.Items.Select(n => n.Action));
        Assert.All(events.Tokens, actual => Assert.Equal(token, actual));
    }

    [Theory]
    [InlineData(NotificationAction.ItemCreated)]
    [InlineData(NotificationAction.ItemUpdated)]
    [InlineData(NotificationAction.ItemDeleted)]
    public async Task Every_mutation_awaits_notification_failure_and_keeps_committed_write(NotificationAction action)
    {
        using var database = new IsolatedDatabase();
        var notifications = new BlockingNotification();
        using var services = database.OpenServices(new TestTimeProvider { UtcNow = DateTime.UtcNow }, s =>
        {
            s.RemoveAll<INotificationHandler<TodoNotification>>();
            s.AddSingleton<INotificationHandler<TodoNotification>>(notifications);
        });
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var token = TestContext.Current.CancellationToken;
        Guid id = Guid.Empty;
        if (action != NotificationAction.ItemCreated)
            id = (await dispatcher.Execute(CreateTodoItemRequest.Make("Before mutation"), token)).ToDoItemId;
        notifications.Armed = true;
        Task pending = action switch
        {
            NotificationAction.ItemCreated => dispatcher.Execute(CreateTodoItemRequest.Make("New item"), token),
            NotificationAction.ItemUpdated => dispatcher.Execute(ToggleCompleteTodoItemRequest.Make(id), token),
            NotificationAction.ItemDeleted => dispatcher.Execute(DeleteTodoItemRequest.Make(id), token),
            _ => throw new InvalidOperationException()
        };
        Assert.False(pending.IsCompleted);
        Assert.Equal(action, notifications.Last!.Action);
        Assert.Equal(token, notifications.LastToken);
        var persisted = services.GetRequiredService<LiteDbProvider>().Database.GetCollection<ToDoItem>(nameof(ToDoItem));
        Assert.Equal(action == NotificationAction.ItemDeleted ? 0 : 1, persisted.Count());
        if (action == NotificationAction.ItemUpdated)
            Assert.True(persisted.FindById(id).IsCompleted);
        var fault = new InvalidOperationException("synthetic failed send");
        notifications.Release.SetException(fault);
        Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));
        var afterFailure = await dispatcher.Execute(GetAllItemsRequest.Make(), token);
        Assert.Equal(action == NotificationAction.ItemDeleted ? 0 : 1, afterFailure.Count);
        if (action == NotificationAction.ItemUpdated)
            Assert.True(Assert.Single(afterFailure).IsCompleted);
    }

    [Fact]
    public async Task Notification_handler_preserves_send_contract_and_observes_and_logs_send_failure()
    {
        var client = new RecordingClient();
        var logger = new RecordingLogger();
        var handler = new NotificationHandler(new StubHubContext(client), logger);
        var dto = new ToDoItemDto { Description = "Broadcast payload", ToDoItemId = Guid.NewGuid() };
        var token = TestContext.Current.CancellationToken;
        var pending = handler.Handle(TodoNotification.Make(NotificationAction.ItemCreated, dto), token);
        Assert.False(pending.IsCompleted);
        Assert.Equal("itemCreated", client.Method);
        Assert.Same(dto, Assert.Single(client.Arguments!));
        Assert.Equal(token, client.Token);
        var failure = new InvalidOperationException("synthetic SignalR send fault");
        client.Release.SetException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));
        Assert.Same(failure, Assert.Single(logger.Errors));
    }

    private sealed class BlockingNotification : INotificationHandler<TodoNotification>
    {
        public bool Armed { get; set; }
        public TodoNotification? Last { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Handle(TodoNotification notification, CancellationToken cancellationToken)
        {
            if (!Armed) return Task.CompletedTask;
            Last = notification;
            LastToken = cancellationToken;
            return Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class TestHubCallerContext(CancellationToken token) : HubCallerContext
    {
        public override string ConnectionId => "isolated-test-client";
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => token;
        public override void Abort() { }
    }

    private sealed class RecordingClient : IClientProxy
    {
        public string? Method { get; private set; }
        public object?[]? Arguments { get; private set; }
        public CancellationToken Token { get; private set; }
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Method = method;
            Arguments = args;
            Token = cancellationToken;
            return Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class StubHubContext(IClientProxy client) : IHubContext<ToDoHub>
    {
        public IHubClients Clients { get; } = new StubHubClients(client);
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class StubHubClients(IClientProxy client) : IHubClients
    {
        public IClientProxy All => client;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingLogger : ILogger<NotificationHandler>
    {
        public List<Exception> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception is not null) Errors.Add(exception);
        }
    }
}
