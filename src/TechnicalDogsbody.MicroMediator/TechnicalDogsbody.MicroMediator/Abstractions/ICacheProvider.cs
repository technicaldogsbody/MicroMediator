namespace TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Abstraction for cache implementations.
/// Implement this to support FusionCache, distributed cache, etc.
/// </summary>
public interface ICacheProvider
{
    /// <summary>
    /// Attempts to get a cached value.
    /// </summary>
    /// <typeparam name="TResponse">The type of cached value.</typeparam>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="value">The cached value if found.</param>
    /// <returns>True if value was found in cache.</returns>
    bool TryGet<TResponse>(string cacheKey, out TResponse? value);

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    /// <typeparam name="TResponse">The type of value to cache.</typeparam>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="duration">How long to cache the value.</param>
    void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration);

    /// <summary>
    /// Sets a value in the cache with configuration callback.
    /// Allows requests to configure provider-specific options like tags or priority.
    /// </summary>
    /// <typeparam name="TResponse">The type of value to cache.</typeparam>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="duration">How long to cache the value.</param>
    /// <param name="configure">Optional callback to configure provider-specific cache options.</param>
    void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration, Action<ICacheContext>? configure);
}
