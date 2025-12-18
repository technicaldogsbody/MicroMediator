using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

namespace TechnicalDogsbody.MicroMediator;

/// <summary>
/// Core mediator implementation that routes requests to their handlers through a pipeline of behaviors.
/// Uses concurrent dictionary with dynamic dispatch for optimal performance.
/// </summary>
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _provider;

    /// <summary>
    /// Initialises a new instance of the Mediator class.
    /// </summary>
    /// <param name="provider">Service provider for resolving handlers and behaviors.</param>
    /// <exception cref="ArgumentNullException">Thrown when provider is null.</exception>
    public Mediator(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Use static generic cache for optimal performance
        return RequestDispatcher<TResponse>.DispatchAsync(
            _provider,
            request,
            cancellationToken);
    }
}

/// <summary>
/// Static generic cache for request dispatching. Each TResponse gets its own static field.
/// </summary>
/// <typeparam name="TResponse">Response type for caching.</typeparam>
file static class RequestDispatcher<TResponse>
{
    // Each unique TResponse type gets its own concurrent dictionary
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapper<TResponse>> _cache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<TResponse> DispatchAsync(
        IServiceProvider provider,
        IRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        var requestType = request.GetType();

        // Lock-free read from concurrent dictionary
        var wrapper = _cache.GetOrAdd(requestType, static rt =>
        {
            // Create wrapper instance once (happens only on first request of this type)
            var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(rt, typeof(TResponse));
            return (RequestHandlerWrapper<TResponse>)Activator.CreateInstance(wrapperType)!;
        });

        // Dynamic dispatch via virtual method call
        return wrapper.HandleAsync(request, provider, cancellationToken);
    }
}

/// <summary>
/// Base class for request handler wrappers.
/// </summary>
/// <typeparam name="TResponse">Response type.</typeparam>
file abstract class RequestHandlerWrapper<TResponse>
{
    public abstract ValueTask<TResponse> HandleAsync(
        IRequest<TResponse> request,
        IServiceProvider provider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Concrete wrapper implementation that handles the request with its handler and pipeline.
/// Caches handler and behavior resolution for maximum performance.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
file sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    // Cache handler and behaviors after first resolution (avoids DI lookup on every request)
    private IRequestHandler<TRequest, TResponse>? _cachedHandler;
    private IPipelineBehavior<TRequest, TResponse>[]? _cachedBehaviors;
    private bool _initialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask<TResponse> HandleAsync(
        IRequest<TResponse> request,
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        // Cast once at the wrapper level (type-safe due to generic constraints)
        var typedRequest = (TRequest)request;

        // Initialize cache on first call (thread-safe via GetOrAdd pattern above)
        if (!_initialized)
        {
            InitializeCache(provider);
        }

        var handler = _cachedHandler!;
        var behaviors = _cachedBehaviors!;

        // Fast path: no behaviors, call handler directly
        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(typedRequest, cancellationToken);
        }

        // Build pipeline inline (minimize allocations)
        return ExecutePipeline(typedRequest, handler, behaviors, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitializeCache(IServiceProvider provider)
    {
        _cachedHandler = provider.GetService<IRequestHandler<TRequest, TResponse>>()
            ?? throw new InvalidOperationException(
                $"No handler registered for request type '{typeof(TRequest).Name}'. " +
                $"Register an implementation of IRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}>.");

        // Get behaviors in reverse order (execution order is reverse of registration)
        var behaviorList = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
        Array.Reverse(behaviorList);
        _cachedBehaviors = behaviorList;

        _initialized = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<TResponse> ExecutePipeline(
        TRequest request,
        IRequestHandler<TRequest, TResponse> handler,
        IPipelineBehavior<TRequest, TResponse>[] behaviors,
        CancellationToken cancellationToken)
    {
        // Build pipeline delegate chain
        RequestHandlerDelegate<TResponse> handlerDelegate = () =>
            handler.HandleAsync(request, cancellationToken);

        // Reverse iteration to build pipeline from innermost to outermost
        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = handlerDelegate;
            handlerDelegate = () => behavior.HandleAsync(request, next, cancellationToken);
        }

        return handlerDelegate();
    }
}
