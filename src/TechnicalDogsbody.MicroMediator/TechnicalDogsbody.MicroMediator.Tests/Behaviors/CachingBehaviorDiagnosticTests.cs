using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

namespace TechnicalDogsbody.MicroMediator.Tests.Behaviors;

public class CachingBehaviorDiagnosticTests
{
    [Fact]
    public void CachingBehavior_IsRegisteredCorrectly()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddMediator()
            .AddDefaultCachingPipeline();

        var provider = services.BuildServiceProvider();

        // Check if open generic is registered
        var openGenericBehaviors = services.Where(sd =>
            sd.ServiceType == typeof(IPipelineBehavior<,>)).ToList();

        Assert.NotEmpty(openGenericBehaviors);

        // Try to resolve a closed generic
        var closedBehaviors = provider.GetServices<IPipelineBehavior<TestCacheableRequest, string>>();
        Assert.NotEmpty(closedBehaviors);
    }

    [Fact]
    public async Task CachingBehavior_DirectTest_WorksCorrectly()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var behavior = new TechnicalDogsbody.MicroMediator.Behaviors.CachingBehavior<TestCacheableRequest, string>(cache);

        int callCount = 0;
        RequestHandlerDelegate<string> handler = () =>
        {
            callCount++;
            return ValueTask.FromResult("result");
        };

        var request = new TestCacheableRequest();

        // First call
        string result1 = await behavior.HandleAsync(request, handler, CancellationToken.None);
        Assert.Equal("result", result1);
        Assert.Equal(1, callCount);

        // Check cache
        Assert.True(cache.TryGetValue("test-key", out string? cached));
        Assert.Equal("result", cached);

        // Second call
        string result2 = await behavior.HandleAsync(request, handler, CancellationToken.None);
        Assert.Equal("result", result2);
        Assert.Equal(1, callCount); // Should still be 1, not called again
    }

    [ExcludeFromCodeCoverage]
    private record TestCacheableRequest : IRequest<string>, ICacheableRequest
    {
        public string CacheKey => "test-key";
        public TimeSpan? CacheDuration => null;
    }
}
