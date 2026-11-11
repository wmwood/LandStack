using LandStack.Api.Infrastructure.Data.Models;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Features;
using LandStack.Api.Infrastructure.Services;
using LandStack.Api.Infrastructure.Dispatcher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

// Database instances are isolated; serial execution also keeps fault-injection tests deterministic.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace LandStack.Api.Tests;

public sealed class PersistenceRegressionTests
{
    [Fact]
    public async Task Shared_handlers_create_toggle_and_delete_with_existing_notifications()
    {
        using var database = new IsolatedDatabase();
        var now = new DateTime(2026, 11, 11, 12, 34, 56, DateTimeKind.Utc);
        var clock = new TestTimeProvider { UtcNow = now };
        using var services = database.OpenServices(clock);
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var notifications = services.GetRequiredService<RecordedNotifications>();

        Assert.Empty(await dispatcher.Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken));
        var created = await dispatcher.Execute(CreateTodoItemRequest.Make("Keep supplied text ✓"), TestContext.Current.CancellationToken);
        Assert.NotEqual(Guid.Empty, created.ToDoItemId);
        Assert.Equal("Keep supplied text ✓", created.Description);
        Assert.False(created.IsCompleted);
        Assert.Equal(now, created.CreatedDate);
        var listed = Assert.Single(await dispatcher.Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken));
        Assert.Equal(created.ToDoItemId, listed.ToDoItemId);
        Assert.Equal(created.Description, listed.Description);
        Assert.Equal(now, listed.CreatedDate.ToUniversalTime());

        await dispatcher.Execute(ToggleCompleteTodoItemRequest.Make(created.ToDoItemId), TestContext.Current.CancellationToken);
        var completed = Assert.Single(await dispatcher.Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken));
        Assert.True(completed.IsCompleted);
        Assert.Equal(created.Description, completed.Description);
        Assert.Equal(now, completed.CreatedDate.ToUniversalTime());

        await dispatcher.Execute(DeleteTodoItemRequest.Make(created.ToDoItemId), TestContext.Current.CancellationToken);
        Assert.Empty(await dispatcher.Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken));
        Assert.Equal(new[] { NotificationAction.ItemCreated, NotificationAction.ItemUpdated, NotificationAction.ItemDeleted },
            notifications.Items.Select(n => n.Action));
        Assert.Equal(created.ToDoItemId, Assert.IsType<ToDoItemDto>(notifications.Items[0].Data).ToDoItemId);
        Assert.True(Assert.IsType<ToDoItem>(notifications.Items[1].Data).IsCompleted);
        Assert.Equal(created.ToDoItemId, Assert.IsType<Guid>(notifications.Items[2].Data));
    }

    [Fact]
    public async Task LiteDb_5_0_11_fixture_preserves_fields_order_and_mutations_across_restart()
    {
        using var database = new IsolatedDatabase("legacy-5.0.11.db");
        var earlyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var lateId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var earlyDate = new DateTime(2021, 2, 3, 10, 20, 30, DateTimeKind.Utc);
        var lateDate = new DateTime(2021, 2, 4, 12, 30, 45, DateTimeKind.Utc);
        var newDate = new DateTime(2026, 11, 11, 12, 34, 56, DateTimeKind.Utc);
        Guid newId;
        using (var services = database.OpenServices(new TestTimeProvider { UtcNow = newDate }))
        {
            var dispatcher = services.GetRequiredService<IDispatcher>();
            var existing = await dispatcher.Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken);
            Assert.Equal(new[] { earlyId, lateId }, existing.Select(i => i.ToDoItemId));
            Assert.Equal("preserved legacy text", existing[0].Description);
            Assert.False(existing[0].IsCompleted);
            Assert.Equal(earlyDate, existing[0].CreatedDate.ToUniversalTime());
            Assert.Equal("Legacy café ✓", existing[1].Description);
            Assert.True(existing[1].IsCompleted);
            Assert.Equal(lateDate, existing[1].CreatedDate.ToUniversalTime());
            await dispatcher.Execute(ToggleCompleteTodoItemRequest.Make(earlyId), TestContext.Current.CancellationToken);
            newId = (await dispatcher.Execute(CreateTodoItemRequest.Make("New after upgrade"), TestContext.Current.CancellationToken)).ToDoItemId;
        }

        using (var services = database.OpenServices(new TestTimeProvider { UtcNow = newDate }))
        {
            var dispatcher = services.GetRequiredService<IDispatcher>();
            var restored = await dispatcher.Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken);
            Assert.Equal(new[] { earlyId, lateId, newId }, restored.Select(i => i.ToDoItemId));
            Assert.True(restored[0].IsCompleted);
            Assert.Equal(earlyDate, restored[0].CreatedDate.ToUniversalTime());
            Assert.Equal(lateDate, restored[1].CreatedDate.ToUniversalTime());
            Assert.Equal("New after upgrade", restored[2].Description);
            Assert.False(restored[2].IsCompleted);
            Assert.Equal(newDate, restored[2].CreatedDate.ToUniversalTime());
            await dispatcher.Execute(DeleteTodoItemRequest.Make(lateId), TestContext.Current.CancellationToken);
        }

        using (var services = database.OpenServices(new TestTimeProvider { UtcNow = newDate }))
        {
            var restored = await services.GetRequiredService<IDispatcher>().Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken);
            Assert.Equal(new[] { earlyId, newId }, restored.Select(i => i.ToDoItemId));
            Assert.Equal("preserved legacy text", restored[0].Description);
        }
    }
}

internal sealed class TestTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; }
    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal sealed class RecordedNotifications : INotificationHandler<TodoNotification>
{
    public List<TodoNotification> Items { get; } = [];
    public List<CancellationToken> Tokens { get; } = [];

    public Task Handle(TodoNotification notification, CancellationToken cancellationToken)
    {
        Items.Add(notification);
        Tokens.Add(cancellationToken);
        return Task.CompletedTask;
    }
}

internal sealed class IsolatedDatabase : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LandStack.Api.Tests", Guid.NewGuid().ToString("N"));

    public string DirectoryPath => _directory;

    public IsolatedDatabase(string? fixture = null)
    {
        Directory.CreateDirectory(_directory);
        if (fixture is not null)
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), Path.Combine(_directory, "database.db"));
    }

    public ServiceProvider OpenServices(TimeProvider timeProvider, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddTodoDispatcher();
        services.RemoveAll<INotificationHandler<TodoNotification>>();
        services.AddSingleton<RecordedNotifications>();
        services.AddSingleton<INotificationHandler<TodoNotification>>(p => p.GetRequiredService<RecordedNotifications>());
        services.AddSingleton(timeProvider);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(_directory));
        services.Configure<LiteDbOptions>(options => options.DataDirectory = _directory);
        services.AddSingleton<LiteDbProvider>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}

internal sealed class TestHostEnvironment(string root) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;
    public string ApplicationName { get; set; } = typeof(Program).Assembly.GetName().Name!;
    public string ContentRootPath { get; set; } = root;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
