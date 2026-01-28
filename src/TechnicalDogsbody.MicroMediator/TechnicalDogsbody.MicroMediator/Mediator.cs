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
/// Caches singleton handlers, resolves scoped/transient handlers per-request.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
file sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    // Cache handler and behaviors when they're singleton
    private readonly ConcurrentDictionary<IServiceProvider, CachedHandlerInfo?> _cache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask<TResponse> HandleAsync(
        IRequest<TResponse> request,
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        // Cast once at the wrapper level (type-safe due to generic constraints)
        var typedRequest = (TRequest)request;

        // Try to get cached handler info
        var cachedInfo = _cache.GetOrAdd(provider, p =>
        {
            // Check handler lifetime
            var handlerType = typeof(IRequestHandler<TRequest, TResponse>);
            var lifetime = HandlerLifetimeTracker.GetLifetime(handlerType);

            if (lifetime == null)
            {
                // Handler not registered through MicroMediator, try to resolve
                var handler = p.GetService<IRequestHandler<TRequest, TResponse>>();
                return handler == null
                    ? throw new InvalidOperationException(
                        $"No handler registered for request type '{typeof(TRequest).Name}'. " +
                        $"Register an implementation of IRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}>.")
                    :
                    // Treat as transient (safe default)
                    null;
            }

            // Only cache singleton handlers
            if (lifetime == ServiceLifetime.Singleton)
            {
                var handler = p.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
                var behaviors = p.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
                Array.Reverse(behaviors);
                return new CachedHandlerInfo(handler, behaviors, true);
            }

            // Don't cache transient/scoped handlers (return null to signal per-request resolution)
            return null;
        });

        // If we have cached singleton handler, use it
        if (cachedInfo != null)
        {
            return ExecuteWithCachedHandler(typedRequest, cachedInfo, cancellationToken);
        }

        // Resolve transient/scoped handler per-request
        return ExecuteWithResolvedHandler(typedRequest, provider, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<TResponse> ExecuteWithCachedHandler(
        TRequest request,
        CachedHandlerInfo cachedInfo,
        CancellationToken cancellationToken)
    {
        var handler = cachedInfo.Handler;
        var behaviors = cachedInfo.Behaviors;

        // Fast path: no behaviors, call handler directly
        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(request, cancellationToken);
        }

        return ExecutePipeline(request, handler, behaviors, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<TResponse> ExecuteWithResolvedHandler(
        TRequest request,
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        // Check if this is a scoped handler
        var handlerType = typeof(IRequestHandler<TRequest, TResponse>);
        var lifetime = HandlerLifetimeTracker.GetLifetime(handlerType);

        // Scoped handlers need to be resolved from a scoped provider
        if (lifetime == ServiceLifetime.Scoped)
        {
            return ExecuteWithScopedHandler(request, provider, cancellationToken);
        }

        // Transient handlers can be resolved directly
        var handler = provider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
        Array.Reverse(behaviors);

        // Fast path: no behaviors, call handler directly
        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(request, cancellationToken);
        }

        return ExecutePipeline(request, handler, behaviors, cancellationToken);
    }

    private static ValueTask<TResponse> ExecuteWithScopedHandler(
        TRequest request,
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        // Try to resolve directly first (works if provider is already scoped, e.g., in ASP.NET Core)
        try
        {
            var handler = provider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
            Array.Reverse(behaviors);

            if (behaviors.Length == 0)
            {
                return handler.HandleAsync(request, cancellationToken);
            }

            return ExecutePipeline(request, handler, behaviors, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot resolve scoped service"))
        {
            // Root provider detected, create scope and dispose after completion
            var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            var handler = scopedProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            var behaviors = scopedProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
            Array.Reverse(behaviors);

            if (behaviors.Length == 0)
            {
                return DisposeAfterAsync(handler.HandleAsync(request, cancellationToken), scope);
            }

            return DisposeAfterAsync(ExecutePipeline(request, handler, behaviors, cancellationToken), scope);
        }
    }

    private static async ValueTask<TResponse> DisposeAfterAsync(
        ValueTask<TResponse> task,
        IServiceScope scope)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            scope.Dispose();
        }
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

    private sealed class CachedHandlerInfo(
        IRequestHandler<TRequest, TResponse> handler,
        IPipelineBehavior<TRequest, TResponse>[] behaviors,
        bool isCached)
    {
        public IRequestHandler<TRequest, TResponse> Handler { get; } = handler;
        public IPipelineBehavior<TRequest, TResponse>[] Behaviors { get; } = behaviors;
        public bool IsCached { get; } = isCached;
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
/// Caches singleton handlers, resolves scoped/transient handlers per-request.
/// </summary>
/// <typeparam name="TRequest">Streaming request type.</typeparam>
/// <typeparam name="TResponse">Response item type.</typeparam>
file sealed class StreamRequestHandlerWrapperImpl<TRequest, TResponse> : StreamRequestHandlerWrapper<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    // Cache handler and behaviors when they're singleton
    private readonly ConcurrentDictionary<IServiceProvider, CachedHandlerInfo?> _cache = new();

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
        // Try to get cached handler info
        var cachedInfo = _cache.GetOrAdd(provider, p =>
        {
            // Check handler lifetime
            var handlerType = typeof(IStreamRequestHandler<TRequest, TResponse>);
            var lifetime = HandlerLifetimeTracker.GetLifetime(handlerType);

            if (lifetime == null)
            {
                // Handler not registered through MicroMediator, try to resolve
                var handler = p.GetService<IStreamRequestHandler<TRequest, TResponse>>();
                return handler == null
                    ? throw new InvalidOperationException(
                        $"No handler registered for streaming request type '{typeof(TRequest).Name}'. " +
                        $"Register an implementation of IStreamRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}>.")
                    :
                    // Treat as transient (safe default)
                    null;
            }

            // Only cache singleton handlers
            if (lifetime == ServiceLifetime.Singleton)
            {
                var handler = p.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
                var behaviors = p.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>().ToArray();
                Array.Reverse(behaviors);
                return new CachedHandlerInfo(handler, behaviors, true);
            }

            // Don't cache transient/scoped handlers
            return null;
        });

        IAsyncEnumerable<TResponse> stream;

        if (cachedInfo != null)
        {
            // Use cached singleton handler
            var handler = cachedInfo.Handler;
            var behaviors = cachedInfo.Behaviors;

            if (behaviors.Length == 0)
            {
                stream = handler.HandleAsync(request, cancellationToken);
            }
            else
            {
                stream = ExecutePipeline(request, handler, behaviors, cancellationToken);
            }

            await foreach (var item in stream.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
        else
        {
            // Check if this is a scoped handler
            var handlerType = typeof(IStreamRequestHandler<TRequest, TResponse>);
            var lifetime = HandlerLifetimeTracker.GetLifetime(handlerType);

            if (lifetime == ServiceLifetime.Scoped)
            {
                // Handle scoped resolution
                await foreach (var item in ExecuteWithScopedStreamHandler(request, provider, cancellationToken))
                {
                    yield return item;
                }
            }
            else
            {
                // Transient handlers can be resolved directly
                var handler = provider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
                var behaviors = provider.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>().ToArray();
                Array.Reverse(behaviors);

                if (behaviors.Length == 0)
                {
                    stream = handler.HandleAsync(request, cancellationToken);
                }
                else
                {
                    stream = ExecutePipeline(request, handler, behaviors, cancellationToken);
                }

                await foreach (var item in stream.WithCancellation(cancellationToken))
                {
                    yield return item;
                }
            }
        }
    }

    private static async IAsyncEnumerable<TResponse> ExecuteWithScopedStreamHandler(
        TRequest request,
        IServiceProvider provider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IServiceScope? scope = null;
        var scopedProvider = provider;
        IAsyncEnumerable<TResponse> stream;

        // Try to resolve directly first (works if provider is already scoped)
        try
        {
            var handler = scopedProvider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
            var behaviors = scopedProvider.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>().ToArray();
            Array.Reverse(behaviors);

            if (behaviors.Length == 0)
            {
                stream = handler.HandleAsync(request, cancellationToken);
            }
            else
            {
                stream = ExecutePipeline(request, handler, behaviors, cancellationToken);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot resolve scoped service"))
        {
            // Root provider detected, create scope
            scope = provider.CreateScope();
            scopedProvider = scope.ServiceProvider;

            var handler = scopedProvider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
            var behaviors = scopedProvider.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>().ToArray();
            Array.Reverse(behaviors);

            if (behaviors.Length == 0)
            {
                stream = handler.HandleAsync(request, cancellationToken);
            }
            else
            {
                stream = ExecutePipeline(request, handler, behaviors, cancellationToken);
            }
        }

        // Enumerate outside of try-catch
        try
        {
            await foreach (var item in stream.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
        finally
        {
            scope?.Dispose();
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

    private sealed class CachedHandlerInfo(
        IStreamRequestHandler<TRequest, TResponse> handler,
        IStreamPipelineBehavior<TRequest, TResponse>[] behaviors,
        bool isCached)
    {
        public IStreamRequestHandler<TRequest, TResponse> Handler { get; } = handler;
        public IStreamPipelineBehavior<TRequest, TResponse>[] Behaviors { get; } = behaviors;
        public bool IsCached { get; } = isCached;
    }
}
