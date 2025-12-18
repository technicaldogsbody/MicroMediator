using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using TechnicalDogsbody.MicroMediator.Abstractions;

namespace TechnicalDogsbody.MicroMediator.Behaviors;

/// <summary>
/// Caches query responses when request implements ICacheableRequest.
/// Register last in the pipeline after logging to only cache validated, successful results.
/// </summary>
/// <typeparam name="TRequest">The type of request being cached.</typeparam>
/// <typeparam name="TResponse">The type of response to cache.</typeparam>
public sealed class CachingBehavior<TRequest, TResponse>(IMemoryCache cache) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (request is not ICacheableRequest cacheableRequest)
        {
            return await next();
        }

        var cacheKey = cacheableRequest.CacheKey;

        // FAST PATH: Cache hit - return synchronously completed ValueTask (zero allocation!)
        if (_cache.TryGetValue(cacheKey, out TResponse? cachedResponse) && cachedResponse is not null)
        {
            return cachedResponse;
        }

        // Slow path: Cache miss - execute handler and cache result
        var response = await next();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = cacheableRequest.CacheDuration ?? TimeSpan.FromMinutes(5)
        };

        _cache.Set(cacheKey, response, cacheOptions);

        return response;
    }
}
