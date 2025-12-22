namespace TechnicalDogsbody.MicroMediator.Examples.Providers;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Example custom cache provider using in-memory dictionary.
/// This demonstrates how to implement ICacheProvider for custom cache backends.
/// In production, you would implement this for FusionCache, Redis, etc.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class CustomDictionaryCacheProvider(ILogger<CustomDictionaryCacheProvider> logger) : ICacheProvider
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public bool TryGet<TResponse>(string cacheKey, out TResponse? value)
    {
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
                value = (TResponse)entry.Value;
                return true;
            }

            logger.LogInformation("Cache expired for key: {CacheKey}", cacheKey);
            _cache.TryRemove(cacheKey, out _);
        }

        logger.LogInformation("Cache miss for key: {CacheKey}", cacheKey);
        value = default;
        return false;
    }

    public void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration)
    {
        var entry = new CacheEntry
        {
            Value = value!,
            ExpiresAt = DateTimeOffset.UtcNow.Add(duration)
        };

        _cache[cacheKey] = entry;
        logger.LogInformation("Cached value for key: {CacheKey}, expires: {ExpiresAt}", 
            cacheKey, entry.ExpiresAt);
    }

    private sealed class CacheEntry
    {
        public required object Value { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
    }
}
