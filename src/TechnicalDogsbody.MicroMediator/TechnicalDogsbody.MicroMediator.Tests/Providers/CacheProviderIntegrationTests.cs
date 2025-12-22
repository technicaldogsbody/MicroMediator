namespace TechnicalDogsbody.MicroMediator.Tests.Providers;

using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

public class CacheProviderIntegrationTests
{
    [Fact]
    public async Task CustomCacheProvider_WorksWithCachingBehavior()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<UniqueCacheableRequest, string, UniqueCacheableRequestHandler>()
            .AddCachingPipeline<TrackingCacheProvider>();

        var provider = services.BuildServiceProvider();
        
        // Debug: Check what's registered
        var cacheProviderFromDI = provider.GetRequiredService<ICacheProvider>();
        Assert.IsType<TrackingCacheProvider>(cacheProviderFromDI);
        
        // Verify behavior is registered
        var behaviors = provider.GetServices<IPipelineBehavior<UniqueCacheableRequest, string>>().ToList();
        Assert.Single(behaviors);
        Assert.IsType<TechnicalDogsbody.MicroMediator.Behaviors.CachingBehavior<UniqueCacheableRequest, string>>(behaviors[0]);
        
        var mediator = provider.GetRequiredService<IMediator>();
        var cacheProvider = cacheProviderFromDI as TrackingCacheProvider;

        Assert.NotNull(cacheProvider);

        var request = new UniqueCacheableRequest { Value = "test" };

        // First call - should miss cache, call handler, then cache result
        string result1 = await mediator.SendAsync(request);
        Assert.Equal("Handled: test", result1);
        Assert.Equal(1, cacheProvider.TryGetCallCount); // Check cache first
        Assert.Equal(1, cacheProvider.SetCallCount); // Then store result

        // Second call - should hit cache, not call handler again
        string result2 = await mediator.SendAsync(request);
        Assert.Equal("Handled: test", result2);
        Assert.Equal(2, cacheProvider.TryGetCallCount); // Check cache again
        Assert.Equal(1, cacheProvider.SetCallCount); // Still 1, no new cache write
    }

    [Fact]
    public async Task DefaultCacheProvider_WorksWithMemoryCache()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<CacheableRequest, string, CacheableRequestHandler>()
            .AddDefaultCachingPipeline();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var request = new CacheableRequest { Value = "test" };

        string result1 = await mediator.SendAsync(request);
        string result2 = await mediator.SendAsync(request);

        Assert.Equal("Handled: test", result1);
        Assert.Equal("Handled: test", result2);
    }

    [Fact]
    public async Task DifferentCacheProviders_CanBeUsedInDifferentScopes()
    {
        var services1 = new ServiceCollection();
        services1.AddMediator()
            .AddHandler<CacheableRequest, string, CacheableRequestHandler>()
            .AddCachingPipeline<TrackingCacheProvider>();

        var services2 = new ServiceCollection();
        services2.AddMediator()
            .AddHandler<CacheableRequest, string, CacheableRequestHandler>()
            .AddDefaultCachingPipeline();

        var provider1 = services1.BuildServiceProvider();
        var provider2 = services2.BuildServiceProvider();

        var mediator1 = provider1.GetRequiredService<IMediator>();
        var mediator2 = provider2.GetRequiredService<IMediator>();

        var request = new CacheableRequest { Value = "test" };

        string result1 = await mediator1.SendAsync(request);
        string result2 = await mediator2.SendAsync(request);

        Assert.Equal("Handled: test", result1);
        Assert.Equal("Handled: test", result2);
    }

    [ExcludeFromCodeCoverage]
    private record CacheableRequest : IRequest<string>, ICacheableRequest
    {
        public required string Value { get; init; }
        public string CacheKey => $"CacheKey-{Value}";
        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
    }

    [ExcludeFromCodeCoverage]
    private class CacheableRequestHandler : IRequestHandler<CacheableRequest, string>
    {
        public ValueTask<string> HandleAsync(CacheableRequest request, CancellationToken cancellationToken) => ValueTask.FromResult($"Handled: {request.Value}");
    }

    [ExcludeFromCodeCoverage]
    private record UniqueCacheableRequest : IRequest<string>, ICacheableRequest
    {
        public required string Value { get; init; }
        public string CacheKey => $"UniqueKey-{Value}";
        public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
    }

    [ExcludeFromCodeCoverage]
    private class UniqueCacheableRequestHandler : IRequestHandler<UniqueCacheableRequest, string>
    {
        public ValueTask<string> HandleAsync(UniqueCacheableRequest request, CancellationToken cancellationToken) => ValueTask.FromResult($"Handled: {request.Value}");
    }

    [ExcludeFromCodeCoverage]
    private class TrackingCacheProvider : ICacheProvider
    {
        private readonly Dictionary<string, object> _cache = new();
        public int TryGetCallCount { get; private set; }
        public int SetCallCount { get; private set; }

        public bool TryGet<TResponse>(string cacheKey, out TResponse? value)
        {
            TryGetCallCount++;
            
            if (_cache.TryGetValue(cacheKey, out object? obj))
            {
                value = (TResponse)obj;
                return true;
            }

            value = default;
            return false;
        }

        public void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration)
        {
            SetCallCount++;
            _cache[cacheKey] = value!;
        }
    }
}
