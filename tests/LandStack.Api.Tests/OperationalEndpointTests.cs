using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using LandStack.Api.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace LandStack.Api.Tests;

public sealed class OperationalEndpointTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public async Task Health_is_liveness_without_database_access_and_OpenAPI_is_Development_only(string environment, bool hasOpenApi)
    {
        using var database = new IsolatedDatabase();
        var databaseActivations = 0;
        await using var factory = new IsolatedApiFactory(database.DirectoryPath, TimeProvider.System, services =>
        {
            services.RemoveAll<LiteDbProvider>();
            services.AddSingleton<LiteDbProvider>(_ =>
            {
                databaseActivations++;
                throw new InvalidOperationException("Liveness must not open a database.");
            });
        }, environment);
        using var http = factory.CreateClient();
        using var health = await http.GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("text/plain", health.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await health.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, databaseActivations);
        using var document = await http.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(hasOpenApi ? HttpStatusCode.OK : HttpStatusCode.NotFound, document.StatusCode);
        Assert.Equal(0, databaseActivations);
        Assert.False(File.Exists(Path.Combine(database.DirectoryPath, "database.db")));
    }

    [Fact]
    public async Task OpenAPI_describes_existing_success_and_problem_responses_and_required_create_contract()
    {
        using var database = new IsolatedDatabase();
        await using var factory = new IsolatedApiFactory(database.DirectoryPath, TimeProvider.System, environment: "Development");
        using var http = factory.CreateClient();
        var doc = await http.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "openapi-v1-Development.json"), doc.GetRawText(), TestContext.Current.CancellationToken);
        Assert.StartsWith("3.", doc.GetProperty("openapi").GetString());
        var paths = doc.GetProperty("paths");
        var listAndCreate = paths.EnumerateObject().Single(p => p.Name.Equals("/ToDoItem", StringComparison.OrdinalIgnoreCase)).Value;
        var list = listAndCreate.GetProperty("get");
        var post = listAndCreate.GetProperty("post");
        AssertResponse(list, "200", "application/json");
        AssertResponse(list, "500", "application/problem+json");
        AssertResponse(post, "201", "application/json");
        AssertResponse(post, "400", "application/problem+json");
        AssertResponse(post, "500", "application/problem+json");
        Assert.True(post.GetProperty("requestBody").GetProperty("required").GetBoolean());
        var input = Resolve(doc, post.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        Assert.Equal("object", input.GetProperty("type").GetString());
        Assert.Contains(input.GetProperty("required").EnumerateArray(), p => p.GetString() == "description");
        var description = input.GetProperty("properties").GetProperty("description");
        Assert.Equal("string", description.GetProperty("type").GetString());
        Assert.Equal(1, description.GetProperty("minLength").GetInt32());
        Assert.Contains("non-whitespace", description.GetProperty("description").GetString());
        var created = Resolve(doc, post.GetProperty("responses").GetProperty("201").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        Assert.Equal(new[] { "createdDate", "description", "isCompleted", "toDoItemId" }, created.GetProperty("properties").EnumerateObject().Select(p => p.Name).Order());
        var date = Resolve(doc, created.GetProperty("properties").GetProperty("createdDate"));
        Assert.Equal("string", date.GetProperty("type").GetString());
        Assert.Equal("date-time", date.GetProperty("format").GetString());
        var toggle = paths.EnumerateObject().Single(p => p.Name.Contains("toggleComplete", StringComparison.OrdinalIgnoreCase)).Value.GetProperty("post");
        AssertResponse(toggle, "200");
        AssertResponse(toggle, "400", "application/problem+json");
        AssertResponse(toggle, "404", "application/problem+json");
        AssertResponse(toggle, "500", "application/problem+json");
        var deletion = paths.EnumerateObject().Single(p => p.Value.TryGetProperty("delete", out _)).Value.GetProperty("delete");
        AssertResponse(deletion, "200");
        AssertResponse(deletion, "400", "application/problem+json");
        AssertResponse(deletion, "500", "application/problem+json");
        Assert.False(deletion.GetProperty("responses").TryGetProperty("404", out _));
        Assert.Contains("absent item also succeeds", deletion.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("  \t\r\n", false)]
    [InlineData("\u0085\u3000", false)]
    [InlineData("Text", true)]
    [InlineData("  Valid supplied text  ", true)]
    [InlineData("\uFEFF", true)]
    public async Task OpenAPI_description_rule_matches_shared_runtime_validation(string description, bool accepted)
    {
        using var database = new IsolatedDatabase();
        await using var factory = new IsolatedApiFactory(database.DirectoryPath, TimeProvider.System, environment: "Development");
        using var http = factory.CreateClient();
        var doc = await http.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);
        var post = doc.GetProperty("paths").EnumerateObject().Single(p => p.Name.Equals("/ToDoItem", StringComparison.OrdinalIgnoreCase)).Value.GetProperty("post");
        var schema = Resolve(doc, post.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        var pattern = schema.GetProperty("properties").GetProperty("description").GetProperty("pattern").GetString()!;
        Assert.Equal(accepted, Regex.IsMatch(description, pattern));
        using var response = await http.PostAsJsonAsync("/todoItem", new { description }, TestContext.Current.CancellationToken);
        Assert.Equal(accepted ? HttpStatusCode.Created : HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static void AssertResponse(JsonElement operation, string status, string? mediaType = null)
    {
        var response = operation.GetProperty("responses").GetProperty(status);
        if (mediaType is not null)
            Assert.True(response.GetProperty("content").TryGetProperty(mediaType, out _));
    }

    private static JsonElement Resolve(JsonElement document, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference)) return schema;
        var target = document;
        foreach (var segment in reference.GetString()![2..].Split('/'))
            target = target.GetProperty(segment.Replace("~1", "/").Replace("~0", "~"));
        return target;
    }
}
