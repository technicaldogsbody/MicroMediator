namespace TechnicalDogsbody.MicroMediator;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TResponse> StreamAsync<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return StreamDispatcher<TResponse>.DispatchAsync(
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
    // Cache per service provider to handle different DI scopes correctly
    private readonly ConcurrentDictionary<IServiceProvider, (IRequestHandler<TRequest, TResponse> Handler, IPipelineBehavior<TRequest, TResponse>[] Behaviors)> _providerCache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask<TResponse> HandleAsync(
        IRequest<TResponse> request,
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        // Cast once at the wrapper level (type-safe due to generic constraints)
        var typedRequest = (TRequest)request;

        // Get or create cached handler and behaviors for this service provider
        var (handler, behaviors) = _providerCache.GetOrAdd(provider, static p =>
        {
            var h = p.GetService<IRequestHandler<TRequest, TResponse>>()
                ?? throw new InvalidOperationException(
                    $"No handler registered for request type '{typeof(TRequest).Name}'. " +
                    $"Register an implementation of IRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}>.");

            // Get behaviors in reverse order (execution order is reverse of registration)
            var behaviorList = p.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
            Array.Reverse(behaviorList);

            return (h, behaviorList);
        });

        // Fast path: no behaviors, call handler directly
        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(typedRequest, cancellationToken);
        }

        // Build pipeline inline (minimize allocations)
        return ExecutePipeline(typedRequest, handler, behaviors, cancellationToken);
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

/// <summary>
/// Static generic cache for streaming request dispatching. Each TResponse gets its own static field.
/// </summary>
/// <typeparam name="TResponse">Response item type for caching.</typeparam>
file static class StreamDispatcher<TResponse>
{
    // Each unique TResponse type gets its own concurrent dictionary
    private static readonly ConcurrentDictionary<Type, StreamRequestHandlerWrapper<TResponse>> _cache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<TResponse> DispatchAsync(
        IServiceProvider provider,
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        var requestType = request.GetType();

        // Lock-free read from concurrent dictionary
        var wrapper = _cache.GetOrAdd(requestType, static rt =>
        {
            // Create wrapper instance once (happens only on first request of this type)
            var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(rt, typeof(TResponse));
            return (StreamRequestHandlerWrapper<TResponse>)Activator.CreateInstance(wrapperType)!;
        });

        // Dynamic dispatch via virtual method call
        return wrapper.HandleAsync(request, provider, cancellationToken);
    }
}

/// <summary>
/// Base class for streaming request handler wrappers.
/// </summary>
/// <typeparam name="TResponse">Response item type.</typeparam>
file abstract class StreamRequestHandlerWrapper<TResponse>
{
    public abstract IAsyncEnumerable<TResponse> HandleAsync(
        IStreamRequest<TResponse> request,
        IServiceProvider provider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Concrete wrapper implementation that handles the streaming request with its handler and pipeline.
/// Caches handler and behavior resolution for maximum performance.
/// </summary>
/// <typeparam name="TRequest">Streaming request type.</typeparam>
/// <typeparam name="TResponse">Response item type.</typeparam>
file sealed class StreamRequestHandlerWrapperImpl<TRequest, TResponse> : StreamRequestHandlerWrapper<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    // Cache per service provider to handle different DI scopes correctly
    private readonly ConcurrentDictionary<IServiceProvider, (IStreamRequestHandler<TRequest, TResponse> Handler, IStreamPipelineBehavior<TRequest, TResponse>[] Behaviors)> _providerCache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override IAsyncEnumerable<TResponse> HandleAsync(
        IStreamRequest<TResponse> request,
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        // Cast once at the wrapper level (type-safe due to generic constraints)
        var typedRequest = (TRequest)request;

        // Return enumerable immediately - actual resolution happens on first MoveNext
        return ExecuteStreamAsync(typedRequest, provider, cancellationToken);
    }

    private async IAsyncEnumerable<TResponse> ExecuteStreamAsync(
        TRequest request,
        IServiceProvider provider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Resolve handler and behaviors lazily (on first enumeration)
        var (handler, behaviors) = _providerCache.GetOrAdd(provider, static p =>
        {
            var h = p.GetService<IStreamRequestHandler<TRequest, TResponse>>()
                ?? throw new InvalidOperationException(
                    $"No handler registered for streaming request type '{typeof(TRequest).Name}'. " +
                    $"Register an implementation of IStreamRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}>.");

            // Get behaviors in reverse order (execution order is reverse of registration)
            var behaviorList = p.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>().ToArray();
            Array.Reverse(behaviorList);

            return (h, behaviorList);
        });

        // Execute the pipeline and yield results
        IAsyncEnumerable<TResponse> stream;
        
        // Fast path: no behaviors, call handler directly
        if (behaviors.Length == 0)
        {
            stream = handler.HandleAsync(request, cancellationToken);
        }
        else
        {
            // Build pipeline inline
            stream = ExecutePipeline(request, handler, behaviors, cancellationToken);
        }

        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IAsyncEnumerable<TResponse> ExecutePipeline(
        TRequest request,
        IStreamRequestHandler<TRequest, TResponse> handler,
        IStreamPipelineBehavior<TRequest, TResponse>[] behaviors,
        CancellationToken cancellationToken)
    {
        // Build pipeline delegate chain
        StreamHandlerDelegate<TResponse> handlerDelegate = () =>
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
