namespace TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Marker interface for requests that should have their responses cached.
/// Implement this interface on your request to enable caching via CachingBehavior.
/// </summary>
public interface ICacheableRequest
{
    /// <summary>
    /// Gets the cache key for this request.
    /// Should be unique for different parameter combinations.
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Gets the cache duration. If null, defaults to 5 minutes.
    /// </summary>
    TimeSpan? CacheDuration { get; }

    /// <summary>
    /// Configures provider-specific cache options before storing in cache.
    /// Override this method to set options like tags, priority, or other provider-specific features.
    /// The default implementation does nothing.
    /// </summary>
    /// <param name="context">Provider-specific cache context to configure.</param>
    void ConfigureCache(ICacheContext context) { }
}
