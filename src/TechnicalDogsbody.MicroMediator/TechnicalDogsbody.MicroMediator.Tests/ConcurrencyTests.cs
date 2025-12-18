using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

namespace TechnicalDogsbody.MicroMediator.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task SendAsync_ConcurrentRequests_AllHandled()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<TestRequest, int, TestRequestHandler>();
        
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var tasks = Enumerable.Range(0, 100)
            .Select(i => mediator.SendAsync(new TestRequest { Value = i }))
            .ToArray();

        int[] results = await Task.WhenAll(tasks.Select(t => t.AsTask()));

        Assert.Equal(100, results.Length);
        Assert.All(results, r => Assert.True(r >= 0 && r < 100));
    }

    [Fact]
    public async Task SendAsync_MultipleDifferentRequestTypes_AllHandled()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<FirstRequest, string, FirstRequestHandler>()
            .AddHandler<SecondRequest, int, SecondRequestHandler>();
        
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var task1 = mediator.SendAsync(new FirstRequest());
        var task2 = mediator.SendAsync(new SecondRequest());

        string result1 = await task1;
        int result2 = await task2;

        Assert.Equal("First", result1);
        Assert.Equal(42, result2);
    }

    [Fact]
    public async Task SendAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<CancellableRequest, string, CancellableRequestHandler>();
        
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            mediator.SendAsync(new CancellableRequest(), cts.Token).AsTask());
        
        Assert.True(exception is TaskCanceledException or OperationCanceledException);
    }

    [Fact]
    public async Task SendAsync_SameRequestMultipleTimes_CachesWrapperCorrectly()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<TestRequest, int, TestRequestHandler>();
        
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        for (int i = 0; i < 10; i++)
        {
            int result = await mediator.SendAsync(new TestRequest { Value = i });
            Assert.Equal(i, result);
        }
    }

    [Fact]
    public async Task SendAsync_WithValueTaskReturns_WorksCorrectly()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<SyncRequest, string, SyncRequestHandler>();
        
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.SendAsync(new SyncRequest());

        Assert.Equal("Sync", result);
    }

    [ExcludeFromCodeCoverage]
    private record TestRequest : IRequest<int>
    {
        public required int Value { get; init; }
    }

    [ExcludeFromCodeCoverage]
    private class TestRequestHandler : IRequestHandler<TestRequest, int>
    {
        public ValueTask<int> HandleAsync(TestRequest request, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(request.Value);
        }
    }

    [ExcludeFromCodeCoverage]
    private record FirstRequest : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private class FirstRequestHandler : IRequestHandler<FirstRequest, string>
    {
        public ValueTask<string> HandleAsync(FirstRequest request, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult("First");
        }
    }

    [ExcludeFromCodeCoverage]
    private record SecondRequest : IRequest<int>;

    [ExcludeFromCodeCoverage]
    private class SecondRequestHandler : IRequestHandler<SecondRequest, int>
    {
        public ValueTask<int> HandleAsync(SecondRequest request, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(42);
        }
    }

    [ExcludeFromCodeCoverage]
    private record CancellableRequest : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private class CancellableRequestHandler : IRequestHandler<CancellableRequest, string>
    {
        public async ValueTask<string> HandleAsync(CancellableRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(1000, cancellationToken);
            return "Done";
        }
    }

    [ExcludeFromCodeCoverage]
    private record SyncRequest : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private class SyncRequestHandler : IRequestHandler<SyncRequest, string>
    {
        public ValueTask<string> HandleAsync(SyncRequest request, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult("Sync");
        }
    }
}
