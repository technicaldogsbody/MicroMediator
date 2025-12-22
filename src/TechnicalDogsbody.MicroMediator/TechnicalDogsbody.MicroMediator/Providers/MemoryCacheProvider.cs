using Microsoft.Extensions.Caching.Memory;
using TechnicalDogsbody.MicroMediator.Abstractions;

namespace TechnicalDogsbody.MicroMediator.Providers;

/// <summary>
/// IMemoryCache implementation of ICacheProvider.
/// Default cache provider for in-memory caching.
/// </summary>
public sealed class MemoryCacheProvider(IMemoryCache cache) : ICacheProvider
{
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <inheritdoc />
    public bool TryGet<TResponse>(string cacheKey, out TResponse? value)
    {
        return _cache.TryGetValue(cacheKey, out value);
    }

    /// <inheritdoc />
    public void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration
        };

        _cache.Set(cacheKey, value, cacheOptions);
    }
}
