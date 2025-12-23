
namespace TechnicalDogsbody.MicroMediator.Behaviors;

using System.Runtime.CompilerServices;
using TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Caches query responses when request implements ICacheableRequest.
/// Register last in the pipeline after logging to only cache validated, successful results.
/// </summary>
/// <typeparam name="TRequest">The type of request being cached.</typeparam>
/// <typeparam name="TResponse">The type of response to cache.</typeparam>
public sealed class CachingBehavior<TRequest, TResponse>(ICacheProvider cacheProvider) : IPipelineBehavior<TRequest, TResponse>
where TRequest : IRequest<TResponse>
{
    private readonly ICacheProvider _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));

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

        string cacheKey = cacheableRequest.CacheKey;

        // FAST PATH: Cache hit - return synchronously completed ValueTask (zero allocation!)
        if (_cacheProvider.TryGet<TResponse>(cacheKey, out var cachedResponse) && cachedResponse is not null)
        {
            return cachedResponse;
        }

        // Slow path: Cache miss - execute handler and cache result
        var response = await next();

        var duration = cacheableRequest.CacheDuration ?? TimeSpan.FromMinutes(5);
        _cacheProvider.Set(cacheKey, response, duration);

        return response;
    }
}
