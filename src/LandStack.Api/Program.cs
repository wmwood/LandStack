using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using LandStack.Api.Hubs;
using LandStack.Api.Controllers;
using LandStack.Api.Infrastructure.Dto;
using LandStack.Api.Infrastructure.Dispatcher;
using LandStack.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins("http://localhost:8080")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer((schema, context, _) =>
    {
        if (context.JsonTypeInfo.Type == typeof(DateTime))
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = "date-time";
        }
        if (context.JsonTypeInfo.Type == typeof(CreateTodoItemDto))
        {
            schema.Type = JsonSchemaType.Object;
            schema.Required ??= new HashSet<string>();
            schema.Required.Add("description");
            // Match .NET whitespace validation without changing runtime binding or MTT contracts.
            schema.Properties!["description"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                Pattern = @"[^\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]",
                Description = "Must contain at least one non-whitespace character. Valid text is preserved as supplied."
            };
        }
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        if (context.Description.ActionDescriptor is ControllerActionDescriptor action
            && action.ControllerTypeInfo.AsType() == typeof(ToDoItemController)
            && action.MethodInfo.Name == nameof(ToDoItemController.Post)
            && operation.RequestBody is OpenApiRequestBody requestBody)
        {
            requestBody.Required = true;
            foreach (var mediaType in requestBody.Content?.Values.OfType<OpenApiMediaType>() ?? [])
            {
                if (mediaType.Schema is OpenApiSchema { OneOf.Count: 2 } nullableBody)
                    mediaType.Schema = nullableBody.OneOf.Single(candidate => candidate.Type != JsonSchemaType.Null);
            }
        }
        return Task.CompletedTask;
    });
});
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TodoExceptionHandler>();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false;
    options.AddFilter<TodoHubFilter>();
});
builder.Services.AddTodoDispatcher();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.Configure<LiteDbOptions>(builder.Configuration.GetSection("LiteDb"));
builder.Services.AddSingleton<LiteDbProvider>();

var app = builder.Build();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseRouting();
app.UseCors();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<ToDoHub>("/todohub");
app.Run();

// Public entry point for isolated application-host regression tests.
public partial class Program { }
