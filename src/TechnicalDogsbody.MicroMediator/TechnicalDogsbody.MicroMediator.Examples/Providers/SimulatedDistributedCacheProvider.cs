namespace TechnicalDogsbody.MicroMediator.Examples.Providers;

using System.Collections.Concurrent;
using TechnicalDogsbody.MicroMediator.Abstractions;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Example cache provider simulating distributed cache behaviour.
/// This demonstrates:
/// - How to implement ICacheProvider for external cache systems (Redis, Memcached, etc.)
/// - Serialization/deserialization patterns
/// - Connection handling
/// - Error handling strategies
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SimulatedDistributedCacheProvider : ICacheProvider
{
    private readonly ConcurrentDictionary<string, CachedItem> _distributedStore = new();
    private readonly ILogger<SimulatedDistributedCacheProvider> _logger;

    public SimulatedDistributedCacheProvider(ILogger<SimulatedDistributedCacheProvider> logger)
    {
        _logger = logger;
        _logger.LogInformation("Initialized simulated distributed cache provider");
    }

    public bool TryGet<TResponse>(string cacheKey, out TResponse? value)
    {
        try
        {
            if (_distributedStore.TryGetValue(cacheKey, out var item))
            {
                if (item.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    _logger.LogDebug("Distributed cache HIT: {CacheKey}", cacheKey);
                    
                    value = (TResponse)item.Value;
                    return true;
                }

                _logger.LogDebug("Distributed cache EXPIRED: {CacheKey}", cacheKey);
                _distributedStore.TryRemove(cacheKey, out _);
            }

            _logger.LogDebug("Distributed cache MISS: {CacheKey}", cacheKey);
            value = default;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from distributed cache: {CacheKey}", cacheKey);
            value = default;
            return false;
        }
    }

    public void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration)
    {
        try
        {
            var item = new CachedItem
            {
                Value = value!,
                ExpiresAt = DateTimeOffset.UtcNow.Add(duration),
                CachedAt = DateTimeOffset.UtcNow
            };

            _distributedStore[cacheKey] = item;

            _logger.LogDebug("Stored in distributed cache: {CacheKey}, TTL: {Duration}", 
                cacheKey, duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing in distributed cache: {CacheKey}", cacheKey);
        }
    }

    private sealed class CachedItem
    {
        public required object Value { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public required DateTimeOffset CachedAt { get; init; }
    }
}
