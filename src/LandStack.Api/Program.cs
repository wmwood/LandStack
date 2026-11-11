using System;
using System.Text.Json.Serialization;
using LandStack.Api.Hubs;
using LandStack.Api.Infrastructure.Dispatcher;
using LandStack.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
app.MapControllers();
app.MapHub<ToDoHub>("/todohub");
app.Run();

// Public entry point for isolated application-host regression tests.
public partial class Program { }
