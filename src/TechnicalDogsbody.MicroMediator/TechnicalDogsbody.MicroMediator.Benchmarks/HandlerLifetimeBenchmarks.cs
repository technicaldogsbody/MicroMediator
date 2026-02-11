namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Benchmarks comparing performance across different handler lifetimes.
/// </summary>
[MemoryDiagnoser]
public class HandlerLifetimeBenchmarks
{
    private IServiceProvider _singletonProvider = null!;
    private IServiceProvider _scopedProvider = null!;
    private IServiceProvider _transientProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup for Singleton handlers
        var singletonServices = new ServiceCollection();
        singletonServices
            .AddMediator()
            .AddSingletonHandler<SingletonTestQuery, string, SingletonTestQueryHandler>();
        _singletonProvider = singletonServices.BuildServiceProvider();

        // Setup for Scoped handlers
        var scopedServices = new ServiceCollection();
        scopedServices
            .AddMediator()
            .AddScopedHandler<ScopedTestQuery, string, ScopedTestQueryHandler>();
        scopedServices.AddScoped<ScopedDependency>();
        _scopedProvider = scopedServices.BuildServiceProvider();

        // Setup for Transient handlers
        var transientServices = new ServiceCollection();
        transientServices
            .AddMediator()
            .AddTransientHandler<TransientTestQuery, string, TransientTestQueryHandler>();
        _transientProvider = transientServices.BuildServiceProvider();
    }

    [Benchmark(Baseline = true)]
    public async Task<string> Singleton_Handler()
    {
        var mediator = _singletonProvider.GetRequiredService<IMediator>();
        return await mediator.SendAsync(new SingletonTestQuery());
    }

    [Benchmark]
    public async Task<string> Scoped_Handler_WithScope()
    {
        // Create scope manually to simulate ASP.NET Core request scope
        using var scope = _scopedProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.SendAsync(new ScopedTestQuery());
    }

    [Benchmark]
    public async Task<string> Transient_Handler()
    {
        var mediator = _transientProvider.GetRequiredService<IMediator>();
        return await mediator.SendAsync(new TransientTestQuery());
    }

    [Benchmark]
    public async Task<string> Scoped_Handler_RootProvider()
    {
        // Call from root provider (forces scope creation inside MicroMediator)
        var mediator = _scopedProvider.GetRequiredService<IMediator>();
        return await mediator.SendAsync(new ScopedTestQuery());
    }

    // Test requests and handlers
    public record SingletonTestQuery : IRequest<string>;
    public record ScopedTestQuery : IRequest<string>;
    public record TransientTestQuery : IRequest<string>;

    public class SingletonTestQueryHandler : IRequestHandler<SingletonTestQuery, string>
    {
        public ValueTask<string> HandleAsync(SingletonTestQuery request, CancellationToken cancellationToken) => ValueTask.FromResult("Result");
    }

    public class ScopedTestQueryHandler(ScopedDependency dependency) : IRequestHandler<ScopedTestQuery, string>
    {
        public ValueTask<string> HandleAsync(ScopedTestQuery request, CancellationToken cancellationToken) => ValueTask.FromResult(dependency.GetValue());
    }

    public class TransientTestQueryHandler : IRequestHandler<TransientTestQuery, string>
    {
        public ValueTask<string> HandleAsync(TransientTestQuery request, CancellationToken cancellationToken) => ValueTask.FromResult("Result");
    }

    public class ScopedDependency
    {
        private readonly string _value = Guid.NewGuid().ToString();

        public string GetValue() => _value;
    }
}
