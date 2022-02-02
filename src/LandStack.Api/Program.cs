using System;
using System.IO;
using LandStack.Api.Hubs;
using LandStack.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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

builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.Converters.Add(new StringEnumConverter());
    options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
    options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
});

builder.Services.AddSignalR();

builder.Services.AddSingleton<IClock, Clock>();
builder.Services.AddSingleton<ToDoItemService, ToDoItemService>();
builder.Services.AddSingleton<LiteDbProvider, LiteDbProvider>();
builder.Services.AddSingleton<IToDoItemGateway, ToDoItemLiteDbGateway>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

AppDomain.CurrentDomain.SetData("DataDirectory", Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

app.UseCors();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<ToDoHub>("/todohub");
});


app.Run();