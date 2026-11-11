using LandStack.Api.Infrastructure.Dispatcher;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TodoDispatcher = LandStack.Api.Infrastructure.Dispatcher.Dispatcher;

namespace LandStack.Api.Tests;

public sealed class DispatcherTests
{
    [Fact]
    public async Task Executes_response_and_void_handlers_with_same_scoped_dependencies_and_token()
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var probe = scope.ServiceProvider.GetRequiredService<Probe>();
        var token = TestContext.Current.CancellationToken;
        IRequest<Guid> request = new ReadScope();
        Assert.Equal(probe.Id, await dispatcher.Execute(request, token));
        await dispatcher.Execute(new Increment(), token);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(token, probe.LastToken);
        using var otherScope = services.CreateScope();
        Assert.NotEqual(probe.Id, await otherScope.ServiceProvider.GetRequiredService<IDispatcher>().Execute(new ReadScope(), token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejects_missing_or_duplicate_response_handlers(bool duplicate)
    {
        var registrations = new ServiceCollection();
        registrations.AddScoped<IDispatcher, TodoDispatcher>();
        registrations.AddScoped<Probe>();
        if (duplicate)
        {
            registrations.AddScoped<IRequestHandler<ReadScope, Guid>, ReadScopeHandler>();
            registrations.AddScoped<IRequestHandler<ReadScope, Guid>, ReadScopeHandler>();
        }
        using var services = registrations.BuildServiceProvider();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await services.GetRequiredService<IDispatcher>().Execute(new ReadScope(), TestContext.Current.CancellationToken));
        Assert.Contains("Expected exactly one", error.Message);
        Assert.Contains(duplicate ? "found 2" : "found 0", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejects_missing_or_duplicate_void_handlers(bool duplicate)
    {
        var registrations = new ServiceCollection();
        registrations.AddScoped<IDispatcher, TodoDispatcher>();
        registrations.AddScoped<Probe>();
        if (duplicate)
        {
            registrations.AddScoped<IRequestHandler<Increment>, IncrementHandler>();
            registrations.AddScoped<IRequestHandler<Increment>, IncrementHandler>();
        }
        using var services = registrations.BuildServiceProvider();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await services.GetRequiredService<IDispatcher>().Execute(new Increment(), TestContext.Current.CancellationToken));
        Assert.Contains(duplicate ? "found 2" : "found 0", error.Message);
    }

    [Fact]
    public async Task Precancelled_dispatch_does_not_invoke_request_or_notification_handlers()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var notifications = 0;
        using var services = CreateServices(s => s.AddSingleton<INotificationHandler<Notice>>(
            new NoticeHandler((_, _) => { notifications++; return Task.CompletedTask; })));
        var dispatcher = services.GetRequiredService<IDispatcher>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await dispatcher.Execute(new ReadScope(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await dispatcher.Execute(new Increment(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await dispatcher.Notify(new Notice(), cancellation.Token));
        Assert.Equal(0, services.GetRequiredService<Probe>().Calls);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task Notification_without_handlers_completes_successfully()
    {
        using var services = CreateServices();
        await services.GetRequiredService<IDispatcher>().Notify(new Notice(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Notification_invokes_every_handler_and_awaits_remaining_work_even_after_sync_fault()
    {
        var token = TestContext.Current.CancellationToken;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoked = new List<CancellationToken>();
        var failure = new InvalidOperationException("synthetic notification fault");
        using var services = CreateServices(s =>
        {
            s.AddSingleton<INotificationHandler<Notice>>(new NoticeHandler((_, cancellation) =>
            {
                invoked.Add(cancellation);
                throw failure;
            }));
            s.AddSingleton<INotificationHandler<Notice>>(new NoticeHandler(async (_, cancellation) =>
            {
                invoked.Add(cancellation);
                await release.Task.WaitAsync(cancellation);
            }));
        });
        var pending = services.GetRequiredService<IDispatcher>().Notify(new Notice(), token);
        Assert.Equal(new[] { token, token }, invoked);
        Assert.False(pending.IsCompleted);
        release.SetResult();
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending));
    }

    [Fact]
    public async Task Notification_awaits_asynchronous_failure_and_forwards_cancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var services = CreateServices(s => s.AddSingleton<INotificationHandler<Notice>>(new NoticeHandler(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        })));
        var pending = services.GetRequiredService<IDispatcher>().Notify(new Notice(), cancellation.Token);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
    }

    [Fact]
    public async Task Synchronous_handler_fault_is_not_wrapped_in_reflection_exception()
    {
        var failure = new InvalidOperationException("synthetic request fault");
        using var services = CreateServices(s => s.AddSingleton<IRequestHandler<Fail>>(new FaultHandler(failure)));
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await services.GetRequiredService<IDispatcher>().Execute(new Fail(), TestContext.Current.CancellationToken)));
    }

    private static ServiceProvider CreateServices(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, TodoDispatcher>();
        services.AddScoped<Probe>();
        services.AddScoped<IRequestHandler<ReadScope, Guid>, ReadScopeHandler>();
        services.AddScoped<IRequestHandler<Increment>, IncrementHandler>();
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private sealed record ReadScope : IRequest<Guid>;
    private sealed record Increment : IRequest;
    private sealed record Fail : IRequest;
    private sealed record Notice : INotification;
    private sealed class Probe
    {
        public Guid Id { get; } = Guid.NewGuid();
        public int Calls { get; set; }
        public CancellationToken LastToken { get; set; }
    }
    private sealed class ReadScopeHandler(Probe probe) : IRequestHandler<ReadScope, Guid>
    {
        Task<Guid> IRequestHandler<ReadScope, Guid>.Handle(ReadScope request, CancellationToken cancellationToken)
        {
            probe.LastToken = cancellationToken;
            return Task.FromResult(probe.Id);
        }
    }
    private sealed class IncrementHandler(Probe probe) : IRequestHandler<Increment>
    {
        public Task Handle(Increment request, CancellationToken cancellationToken)
        {
            probe.Calls++;
            probe.LastToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
    private sealed class NoticeHandler(Func<Notice, CancellationToken, Task> handler) : INotificationHandler<Notice>
    {
        Task INotificationHandler<Notice>.Handle(Notice notification, CancellationToken cancellationToken) => handler(notification, cancellationToken);
    }
    private sealed class FaultHandler(Exception failure) : IRequestHandler<Fail>
    {
        public Task Handle(Fail request, CancellationToken cancellationToken) => throw failure;
    }
}
