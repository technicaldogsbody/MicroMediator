
namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[ExcludeFromCodeCoverage]
public class CachingBenchmarks
{
    private ServiceProvider _simpleMediatorProvider = null!;
    private IMediator _simpleMediator = null!;
    private int _counter;

    [GlobalSetup]
    public void Setup()
    {
        var simpleServices = new ServiceCollection();
        simpleServices.AddMemoryCache();
        simpleServices
            .AddMediator()
            .AddHandler<CachedQuery, int, CachedQueryHandler>()
            .AddDefaultCachingPipeline();

        _simpleMediatorProvider = simpleServices.BuildServiceProvider();
        _simpleMediator = _simpleMediatorProvider.GetRequiredService<IMediator>();
        _counter = 0;
    }

    [GlobalCleanup]
    public void Cleanup() => _simpleMediatorProvider?.Dispose();

    [Benchmark]
    public async Task<int> CacheHit() => await _simpleMediator.SendAsync(new CachedQuery { Id = 1 });

    [Benchmark]
    public async Task<int> CacheMiss() => await _simpleMediator.SendAsync(new CachedQuery { Id = Interlocked.Increment(ref _counter) });

    public record CachedQuery : IRequest<int>, ICacheableRequest
    {
        public int Id { get; init; }
        public string CacheKey => $"cached-{Id}";
        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
    }

    public class CachedQueryHandler : IRequestHandler<CachedQuery, int>
    {
        public ValueTask<int> HandleAsync(CachedQuery request, CancellationToken cancellationToken) => ValueTask.FromResult(request.Id * 2);
    }
}
