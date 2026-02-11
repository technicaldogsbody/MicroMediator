
namespace TechnicalDogsbody.MicroMediator.Providers;

using Microsoft.Extensions.Caching.Memory;
using TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// IMemoryCache implementation of ICacheProvider.
/// Default cache provider for in-memory caching.
/// </summary>
public sealed class MemoryCacheProvider(IMemoryCache cache) : ICacheProvider
{
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <inheritdoc />
    public bool TryGet<TResponse>(string cacheKey, out TResponse? value) => _cache.TryGetValue(cacheKey, out value);

    /// <inheritdoc />
    public void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration
        };

        _cache.Set(cacheKey, value, cacheOptions);
    }

    /// <summary>
    /// Sets a value in the cache with configuration callback.
    /// Allows configuration of sliding expiration, absolute expiration, priority, and size.
    /// </summary>
    /// <typeparam name="TResponse">The type of value to cache.</typeparam>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="duration">How long to cache the value.</param>
    /// <param name="configure">Optional callback to configure cache options.</param>
    public void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration, Action<ICacheContext>? configure)
    {
        var context = new MemoryCacheContext();
        configure?.Invoke(context);

        var options = new MemoryCacheEntryOptions
        {
            Priority = context.Priority
        };

        // Only set one absolute expiration type
        if (context.AbsoluteExpiration.HasValue)
        {
            options.AbsoluteExpiration = context.AbsoluteExpiration;
        }
        else
        {
            options.AbsoluteExpirationRelativeToNow = duration;
        }

        if (context.SlidingExpiration.HasValue)
        {
            options.SlidingExpiration = context.SlidingExpiration;
        }

        _cache.Set(cacheKey, value, options);
    }
}
