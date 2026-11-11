using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using LandStack.Api.Infrastructure.Data.Models;
using LandStack.Api.Infrastructure.Dispatcher;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Features;
using LandStack.Api.Infrastructure.Services;
using LiteDB;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LandStack.Api.Tests;

public sealed class CorrectnessTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 11, 12, 34, 56, TimeSpan.Zero);
    private const string ValidationMessage = "Description must contain non-whitespace text.";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t\r\n")]
    public async Task Shared_create_handler_rejects_invalid_text_without_write_or_notification(string? description)
    {
        using var database = new IsolatedDatabase();
        using var services = database.OpenServices(new TestTimeProvider { UtcNow = FixedNow });
        var dispatcher = services.GetRequiredService<IDispatcher>();
        Assert.Equal(ValidationMessage, (await Assert.ThrowsAsync<TodoValidationException>(async () =>
            await dispatcher.Execute(CreateTodoItemRequest.Make(description), TestContext.Current.CancellationToken))).Message);
        Assert.Empty(await dispatcher.Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken));
        Assert.Empty(services.GetRequiredService<RecordedNotifications>().Items);
    }

    [Fact]
    public async Task Missing_toggle_fails_and_absent_delete_succeeds_without_notifications()
    {
        using var database = new IsolatedDatabase();
        using var services = database.OpenServices(new TestTimeProvider { UtcNow = FixedNow });
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var missing = Guid.NewGuid();
        await Assert.ThrowsAsync<TodoItemNotFoundException>(async () =>
            await dispatcher.Execute(ToggleCompleteTodoItemRequest.Make(missing), TestContext.Current.CancellationToken));
        await dispatcher.Execute(DeleteTodoItemRequest.Make(missing), TestContext.Current.CancellationToken);
        Assert.Empty(await dispatcher.Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken));
        Assert.Empty(services.GetRequiredService<RecordedNotifications>().Items);
    }

    [Fact]
    public async Task New_time_is_UTC_and_text_survives_restart_without_changing_global_mapper()
    {
        using var database = new IsolatedDatabase();
        var originalTrim = BsonMapper.Global.TrimWhitespace;
        var originalEmpty = BsonMapper.Global.EmptyStringToNull;
        const string text = "  Preserved text café ✓  ";
        Guid id;
        using (var services = database.OpenServices(new TestTimeProvider { UtcNow = FixedNow }))
        {
            var created = await services.GetRequiredService<IDispatcher>().Execute(CreateTodoItemRequest.Make(text), TestContext.Current.CancellationToken);
            id = created.ToDoItemId;
            Assert.Equal(DateTimeKind.Utc, created.CreatedDate.Kind);
            Assert.Equal(FixedNow.UtcDateTime, created.CreatedDate);
            Assert.Equal(text, created.Description);
        }
        using (var services = database.OpenServices(new TestTimeProvider { UtcNow = FixedNow.AddDays(1) }))
        {
            var restored = Assert.Single(await services.GetRequiredService<IDispatcher>().Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken));
            Assert.Equal(id, restored.ToDoItemId);
            Assert.Equal(text, restored.Description);
            Assert.Equal(FixedNow.UtcDateTime, restored.CreatedDate.ToUniversalTime());
        }
        Assert.Equal(originalTrim, BsonMapper.Global.TrimWhitespace);
        Assert.Equal(originalEmpty, BsonMapper.Global.EmptyStringToNull);
    }

    [Fact]
    public async Task Historical_invalid_descriptions_are_not_normalized_by_read_or_toggle()
    {
        using var database = new IsolatedDatabase();
        using var services = database.OpenServices(new TestTimeProvider { UtcNow = FixedNow });
        var collection = services.GetRequiredService<LiteDbProvider>().Database.GetCollection<BsonDocument>(nameof(ToDoItem));
        string?[] descriptions = [null, "", "   "];
        var ids = new List<Guid>();
        foreach (var description in descriptions)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            collection.Insert(new BsonDocument
            {
                ["_id"] = id,
                ["Description"] = description is null ? BsonValue.Null : description,
                ["IsCompleted"] = false,
                ["CreatedDate"] = FixedNow.UtcDateTime
            });
        }
        var dispatcher = services.GetRequiredService<IDispatcher>();
        var listed = await dispatcher.Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken);
        Assert.Equal(3, listed.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            Assert.Equal(descriptions[i], listed.Single(item => item.ToDoItemId == ids[i]).Description);
            await dispatcher.Execute(ToggleCompleteTodoItemRequest.Make(ids[i]), TestContext.Current.CancellationToken);
            var stored = collection.FindById(ids[i]);
            Assert.Equal(descriptions[i], stored["Description"].IsNull ? null : stored["Description"].AsString);
            Assert.True(stored["IsCompleted"].AsBoolean);
            Assert.Equal(FixedNow.UtcDateTime, stored["CreatedDate"].AsDateTime.ToUniversalTime());
        }
    }

    [Fact]
    public async Task Old_unspecified_clock_fixture_keeps_actual_stored_instant_through_HTTP_hub_toggle_and_restart()
    {
        const string fixture = "legacy-unspecified-5.0.11.db";
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);
        using var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath + ".json", TestContext.Current.CancellationToken));
        var expected = metadata.RootElement.GetProperty("storedUtc").GetDateTimeOffset();
        var sourceTicks = metadata.RootElement.GetProperty("sourceUnspecified").GetDateTime().Ticks;
        Assert.NotEqual(sourceTicks, expected.UtcTicks); // The baseline stored a shifted instant; never retrospectively correct it.
        Assert.Equal(metadata.RootElement.GetProperty("databaseSha256").GetString(),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(fixturePath, TestContext.Current.CancellationToken))).ToLowerInvariant());
        using var database = new IsolatedDatabase(fixture);
        var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        await using (var factory = new IsolatedApiFactory(database.DirectoryPath, new TestTimeProvider { UtcNow = FixedNow }))
        {
            using var http = factory.CreateClient();
            var rest = Assert.Single(await http.GetFromJsonAsync<JsonElement[]>("/todoItem", TestContext.Current.CancellationToken) ?? []);
            Assert.Equal(expected, rest.GetProperty("createdDate").GetDateTimeOffset());
            Assert.EndsWith("Z", rest.GetProperty("createdDate").GetString());
            await using var hub = factory.CreateHub();
            await hub.StartAsync(TestContext.Current.CancellationToken);
            var realtime = Assert.Single(await hub.InvokeAsync<ToDoItemDto[]>("GetAllItems", TestContext.Current.CancellationToken));
            Assert.Equal(expected.UtcDateTime, realtime.CreatedDate.ToUniversalTime());
            await hub.InvokeAsync("ToggleComplete", id, TestContext.Current.CancellationToken);
            var toggled = Assert.Single(await http.GetFromJsonAsync<JsonElement[]>("/todoItem", TestContext.Current.CancellationToken) ?? []);
            Assert.True(toggled.GetProperty("isCompleted").GetBoolean());
            Assert.Equal(expected, toggled.GetProperty("createdDate").GetDateTimeOffset());
        }
        using (var services = database.OpenServices(new TestTimeProvider { UtcNow = FixedNow.AddYears(1) }))
        {
            var restored = Assert.Single(await services.GetRequiredService<IDispatcher>().Execute(GetAllItemsRequest.Make(), TestContext.Current.CancellationToken));
            Assert.Equal(id, restored.ToDoItemId);
            Assert.Equal("Legacy unspecified time", restored.Description);
            Assert.Equal(expected.UtcDateTime, restored.CreatedDate.ToUniversalTime());
            Assert.True(restored.IsCompleted);
        }
    }
    [Theory]
    [InlineData(null, "App_Data")]
    [InlineData("nested/data", "nested/data")]
    public void Database_default_and_relative_configuration_resolve_under_host_content_root(string? configured, string relative)
    {
        using var directory = new IsolatedDatabase();
        using var provider = new LiteDbProvider(Options.Create(new LiteDbOptions { DataDirectory = configured }), new TestHostEnvironment(directory.DirectoryPath));
        provider.Database.GetCollection<BsonDocument>("probe").Insert(new BsonDocument { ["value"] = 1 });
        Assert.True(File.Exists(Path.Combine(directory.DirectoryPath, relative, "database.db")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"description\":null}")]
    [InlineData("{\"description\":\"\"}")]
    [InlineData("{\"description\":\"   \\t\"}")]
    public async Task HTTP_null_missing_and_blank_descriptions_return_shared_validation_problem(string json)
    {
        using var database = new IsolatedDatabase();
        await using var factory = new IsolatedApiFactory(database.DirectoryPath, new TestTimeProvider { UtcNow = FixedNow });
        using var http = factory.CreateClient();
        using var response = await http.PostAsync("/todoItem", new StringContent(json, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal(ValidationMessage, problem.GetProperty("detail").GetString());
        Assert.Empty(await http.GetFromJsonAsync<ToDoItemDto[]>("/todoItem", TestContext.Current.CancellationToken) ?? []);
        Assert.Empty(factory.Notifications.Items);
    }

    [Fact]
    public async Task HTTP_preserves_success_contracts_UTC_dates_and_safe_missing_or_malformed_ID_behavior()
    {
        using var database = new IsolatedDatabase();
        await using var factory = new IsolatedApiFactory(database.DirectoryPath, new TestTimeProvider { UtcNow = FixedNow });
        using var http = factory.CreateClient();
        using var createdResponse = await http.PostAsJsonAsync("/todoItem", new { description = "  Valid text ✓  " }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "createdDate", "description", "isCompleted", "toDoItemId" }, created.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("  Valid text ✓  ", created.GetProperty("description").GetString());
        var id = created.GetProperty("toDoItemId").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(FixedNow, created.GetProperty("createdDate").GetDateTimeOffset());
        Assert.EndsWith("Z", created.GetProperty("createdDate").GetString());
        var listed = Assert.Single(await http.GetFromJsonAsync<JsonElement[]>("/todoItem", TestContext.Current.CancellationToken) ?? []);
        Assert.Equal(FixedNow, listed.GetProperty("createdDate").GetDateTimeOffset());
        Assert.EndsWith("Z", listed.GetProperty("createdDate").GetString());
        Assert.Equal("  Valid text ✓  ", listed.GetProperty("description").GetString());
        using var toggle = await http.PostAsync($"/todoItem/toggleComplete/{id}", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);
        using var deletion = await http.DeleteAsync($"/todoItem/{id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        using var missingDelete = await http.DeleteAsync($"/todoItem/{id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, missingDelete.StatusCode);
        Assert.Equal(3, factory.Notifications.Items.Count);
        using var missingToggle = await http.PostAsync($"/todoItem/toggleComplete/{id}", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingToggle.StatusCode);
        var problem = await missingToggle.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        using var malformed = await http.PostAsync("/todoItem/toggleComplete/not-a-guid", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("application/problem+json", malformed.Content.Headers.ContentType?.MediaType);
        await factory.DisposeAsync();
        using var reopened = new LiteDatabase(Path.Combine(database.DirectoryPath, "database.db"));
        Assert.Equal(0, reopened.GetCollection<ToDoItem>(nameof(ToDoItem)).Count());
    }

    [Fact]
    public async Task Production_HTTP_unexpected_failure_is_generic_problem_even_with_non_JSON_accept()
    {
        using var database = new IsolatedDatabase();
        await using var factory = new IsolatedApiFactory(database.DirectoryPath, new TestTimeProvider { UtcNow = FixedNow }, s =>
        {
            s.RemoveAll<IRequestHandler<GetAllItemsRequest, List<ToDoItemDto>>>();
            s.AddSingleton<IRequestHandler<GetAllItemsRequest, List<ToDoItemDto>>>(new FaultingList());
        });
        using var http = factory.CreateClient();
        http.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        using var response = await http.GetAsync("/todoItem", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("private", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("An unexpected error occurred.", JsonDocument.Parse(body).RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task REST_and_hub_keep_independent_null_enum_and_date_serialization_contracts()
    {
        using var database = new IsolatedDatabase();
        await using var factory = new IsolatedApiFactory(database.DirectoryPath, new TestTimeProvider { UtcNow = FixedNow });
        var rest = factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        var hub = factory.Services.GetRequiredService<IOptions<JsonHubProtocolOptions>>().Value.PayloadSerializerOptions;
        var dto = new ToDoItemDto { Description = null, CreatedDate = DateTime.SpecifyKind(FixedNow.UtcDateTime, DateTimeKind.Unspecified) };
        var restJson = JsonSerializer.SerializeToElement(dto, rest);
        var hubJson = JsonSerializer.SerializeToElement(dto, hub);
        Assert.False(restJson.TryGetProperty("description", out _));
        Assert.Equal(JsonValueKind.Null, hubJson.GetProperty("description").ValueKind);
        Assert.EndsWith("Z", restJson.GetProperty("createdDate").GetString());
        Assert.Equal("2026-11-11T12:34:56", hubJson.GetProperty("createdDate").GetString());
        Assert.Equal("\"ItemCreated\"", JsonSerializer.Serialize(NotificationAction.ItemCreated, rest));
        Assert.Equal("0", JsonSerializer.Serialize(NotificationAction.ItemCreated, hub));
        var local = FixedNow.LocalDateTime;
        Assert.Equal(FixedNow, JsonSerializer.SerializeToElement(new ToDoItemDto { CreatedDate = local }, rest).GetProperty("createdDate").GetDateTimeOffset());
        Assert.Equal(FixedNow, JsonSerializer.SerializeToElement(new ToDoItemDto { CreatedDate = local }, hub).GetProperty("createdDate").GetDateTimeOffset());
    }

    [Fact]
    public async Task In_process_hub_maps_shared_validation_not_found_and_unexpected_errors_safely()
    {
        using var database = new IsolatedDatabase();
        await using var factory = new IsolatedApiFactory(database.DirectoryPath, new TestTimeProvider { UtcNow = FixedNow });
        await using var hub = factory.CreateHub();
        await hub.StartAsync(TestContext.Current.CancellationToken);
        foreach (var description in new string?[] { null, "", "   " })
        {
            var error = await Assert.ThrowsAsync<HubException>(async () =>
                await hub.InvokeAsync("Create", new CreateTodoItemDto { Description = description }, TestContext.Current.CancellationToken));
            Assert.Contains(ValidationMessage, error.Message);
        }
        Assert.Contains(ValidationMessage, (await Assert.ThrowsAsync<HubException>(async () =>
            await hub.InvokeAsync("Create", (CreateTodoItemDto?)null, TestContext.Current.CancellationToken))).Message);
        var missing = Guid.NewGuid();
        Assert.Contains("The to-do item was not found.", (await Assert.ThrowsAsync<HubException>(async () =>
            await hub.InvokeAsync("ToggleComplete", missing, TestContext.Current.CancellationToken))).Message);
        await hub.InvokeAsync("Delete", missing, TestContext.Current.CancellationToken);
        Assert.Empty(await hub.InvokeAsync<ToDoItemDto[]>("GetAllItems", TestContext.Current.CancellationToken));
        Assert.Empty(factory.Notifications.Items);
        var malformed = await Assert.ThrowsAsync<HubException>(async () =>
            await hub.InvokeAsync("ToggleComplete", "not-a-guid", TestContext.Current.CancellationToken));
        Assert.DoesNotContain("stack", malformed.Message, StringComparison.OrdinalIgnoreCase);

        await using var faultFactory = new IsolatedApiFactory(database.DirectoryPath, new TestTimeProvider { UtcNow = FixedNow }, s =>
        {
            s.RemoveAll<IRequestHandler<GetAllItemsRequest, List<ToDoItemDto>>>();
            s.AddSingleton<IRequestHandler<GetAllItemsRequest, List<ToDoItemDto>>>(new FaultingList());
        });
        await using var faultHub = faultFactory.CreateHub();
        await faultHub.StartAsync(TestContext.Current.CancellationToken);
        var unexpected = await Assert.ThrowsAsync<HubException>(async () =>
            await faultHub.InvokeAsync<ToDoItemDto[]>("GetAllItems", TestContext.Current.CancellationToken));
        Assert.Contains("An unexpected error occurred.", unexpected.Message);
        Assert.DoesNotContain("private", unexpected.Message);
        Assert.DoesNotContain("InvalidOperationException", unexpected.Message);
    }

    private sealed class FaultingList : IRequestHandler<GetAllItemsRequest, List<ToDoItemDto>>
    {
        public Task<List<ToDoItemDto>> Handle(GetAllItemsRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("private database path and connection string; stack details");
    }
}

internal sealed class IsolatedApiFactory(string databaseDirectory, TimeProvider timeProvider, Action<IServiceCollection>? configure = null, string environment = "Production")
    : WebApplicationFactory<Program>
{
    private LiteDbProvider? _database;
    public RecordedNotifications Notifications { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["LiteDb:DataDirectory"] = databaseDirectory }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<LiteDbProvider>();
            services.AddSingleton(provider => _database = ActivatorUtilities.CreateInstance<LiteDbProvider>(provider));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(timeProvider);
            services.RemoveAll<INotificationHandler<TodoNotification>>();
            services.AddSingleton<INotificationHandler<TodoNotification>>(Notifications);
            configure?.Invoke(services);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // Wait for any overlapping app.Run shutdown disposal before removing test data.
        _database?.Dispose();
    }

    public HubConnection CreateHub() => new HubConnectionBuilder().WithUrl(new Uri(Server.BaseAddress, "/todohub"), options =>
    {
        options.Transports = HttpTransportType.LongPolling;
        options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
    }).Build();
}
