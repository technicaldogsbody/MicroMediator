namespace TechnicalDogsbody.MicroMediator;

using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechnicalDogsbody.MicroMediator.Abstractions;
using TechnicalDogsbody.MicroMediator.Behaviors;
using TechnicalDogsbody.MicroMediator.Providers;

/// <summary>
/// Extension methods for registering mediator services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the mediator and returns a builder for fluent configuration.
    /// Fully AOT-compatible with zero reflection.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Mediator builder for fluent configuration.</returns>
    public static MediatorBuilder AddMediator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IMediator, Mediator>();
        return new MediatorBuilder(services);
    }
}

/// <summary>
/// Fluent builder for configuring mediator services.
/// </summary>
public sealed class MediatorBuilder
{
    private readonly IServiceCollection _services;
    private bool _validatorsAdded;

    internal MediatorBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Registers a request handler as singleton (default for maximum performance).
    /// Automatically discovers TRequest and TResponse from the handler's implemented interface.
    /// Uses minimal reflection at startup only.
    /// </summary>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// IMPORTANT: Singleton handlers are cached and reused. Only use for handlers with no dependencies
    /// or only singleton dependencies. For handlers with scoped dependencies (DbContext, HttpContext),
    /// use AddScopedHandler instead.
    /// </remarks>
    [Obsolete("Use AddSingletonHandler, AddScopedHandler, or AddTransientHandler to explicitly specify handler lifetime.", false)]
    public MediatorBuilder AddHandler<THandler>()
        where THandler : class => AddHandlerWithLifetime<THandler>(ServiceLifetime.Singleton);

    /// <summary>
    /// Registers a request handler as transient.
    /// Transient handlers are resolved per-request with no caching.
    /// </summary>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddTransientHandler<THandler>()
        where THandler : class => AddHandlerWithLifetime<THandler>(ServiceLifetime.Transient);

    /// <summary>
    /// Registers a request handler as scoped.
    /// Scoped handlers are resolved per-request from the scoped service provider.
    /// Use for handlers with scoped dependencies (DbContext, HttpContext, etc.)
    /// </summary>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddScopedHandler<THandler>()
        where THandler : class => AddHandlerWithLifetime<THandler>(ServiceLifetime.Scoped);

    /// <summary>
    /// Registers a request handler as singleton.
    /// Singleton handlers are cached and reused for maximum performance.
    /// Only use for handlers with no dependencies or only singleton dependencies.
    /// </summary>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddSingletonHandler<THandler>()
        where THandler : class => AddHandlerWithLifetime<THandler>(ServiceLifetime.Singleton);

    private MediatorBuilder AddHandlerWithLifetime<THandler>(ServiceLifetime lifetime)
        where THandler : class
    {
        var handlerType = typeof(THandler);
        var handlerInterfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .ToArray();

        if (handlerInterfaces.Length == 0)
        {
            throw new InvalidOperationException(
                $"Handler type '{handlerType.Name}' must implement IRequestHandler<TRequest, TResponse>.");
        }

        if (handlerInterfaces.Length > 1)
        {
            throw new InvalidOperationException(
                $"Handler type '{handlerType.Name}' implements multiple IRequestHandler interfaces. " +
                $"Use the explicit AddHandler<TRequest, TResponse, THandler>() overload to specify which interface to register.");
        }

        var handlerInterface = handlerInterfaces[0];
        _services.Add(new ServiceDescriptor(handlerInterface, handlerType, lifetime));
        HandlerLifetimeTracker.RegisterHandler(handlerInterface, lifetime);
        return this;
    }

    /// <summary>
    /// Registers a request handler explicitly as singleton (default for maximum performance).
    /// Zero reflection.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// IMPORTANT: Singleton handlers are cached and reused. Only use for handlers with no dependencies
    /// or only singleton dependencies. For handlers with scoped dependencies (DbContext, HttpContext),
    /// use AddScopedHandler instead.
    /// </remarks>
    [Obsolete("Use AddSingletonHandler, AddScopedHandler, or AddTransientHandler to explicitly specify handler lifetime.")]
    public MediatorBuilder AddHandler<TRequest, TResponse, THandler>()
        where TRequest : IRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        _services.AddSingleton<IRequestHandler<TRequest, TResponse>, THandler>();
        HandlerLifetimeTracker.RegisterHandler(typeof(IRequestHandler<TRequest, TResponse>), ServiceLifetime.Singleton);
        return this;
    }

    /// <summary>
    /// Registers a request handler explicitly as transient.
    /// Zero reflection.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddTransientHandler<TRequest, TResponse, THandler>()
        where TRequest : IRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        _services.AddTransient<IRequestHandler<TRequest, TResponse>, THandler>();
        HandlerLifetimeTracker.RegisterHandler(typeof(IRequestHandler<TRequest, TResponse>), ServiceLifetime.Transient);
        return this;
    }

    /// <summary>
    /// Registers a request handler explicitly as scoped.
    /// Zero reflection.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddScopedHandler<TRequest, TResponse, THandler>()
        where TRequest : IRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        _services.AddScoped<IRequestHandler<TRequest, TResponse>, THandler>();
        HandlerLifetimeTracker.RegisterHandler(typeof(IRequestHandler<TRequest, TResponse>), ServiceLifetime.Scoped);
        return this;
    }

    /// <summary>
    /// Registers a request handler explicitly as singleton.
    /// Zero reflection.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddSingletonHandler<TRequest, TResponse, THandler>()
        where TRequest : IRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        _services.AddSingleton<IRequestHandler<TRequest, TResponse>, THandler>();
        return this;
    }

    /// <summary>
    /// Registers a FluentValidation validator by validator type.
    /// Automatically discovers TRequest from the validator's implemented interface.
    /// Automatically adds ValidationBehavior to the pipeline on first validator registration.
    /// Uses minimal reflection at startup only.
    /// </summary>
    /// <typeparam name="TValidator">The validator implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddValidator<TValidator>()
        where TValidator : class
    {
        var validatorType = typeof(TValidator);
        var validatorInterfaces = validatorType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
            .ToArray();

        if (validatorInterfaces.Length == 0)
        {
            throw new InvalidOperationException(
                $"Validator type '{validatorType.Name}' must implement IValidator<T>.");
        }

        if (validatorInterfaces.Length > 1)
        {
            throw new InvalidOperationException(
                $"Validator type '{validatorType.Name}' implements multiple IValidator interfaces. " +
                $"Use the explicit AddValidator<TRequest, TValidator>() overload to specify which interface to register.");
        }

        _services.AddTransient(validatorInterfaces[0], validatorType);

        // Automatically add ValidationBehavior on first validator registration
        if (!_validatorsAdded)
        {
            _services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            _validatorsAdded = true;
        }

        return this;
    }

    /// <summary>
    /// Registers a FluentValidation validator explicitly with all type parameters.
    /// Automatically adds ValidationBehavior to the pipeline on first validator registration.
    /// Zero reflection.
    /// </summary>
    /// <typeparam name="TRequest">The request type being validated.</typeparam>
    /// <typeparam name="TValidator">The validator implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddValidator<TRequest, TValidator>()
        where TValidator : class, IValidator<TRequest>
    {
        _services.AddTransient<IValidator<TRequest>, TValidator>();

        // Automatically add ValidationBehavior on first validator registration
        if (!_validatorsAdded)
        {
            _services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            _validatorsAdded = true;
        }

        return this;
    }

    /// <summary>
    /// Adds the default logging pipeline behavior.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddDefaultLoggingPipeline()
    {
        _services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        return this;
    }

    /// <summary>
    /// Adds the default caching pipeline behavior using IMemoryCache.
    /// Ensures IMemoryCache and MemoryCacheProvider are registered.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddDefaultCachingPipeline()
    {
        _services.TryAddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions()));
        _services.TryAddSingleton<ICacheProvider, MemoryCacheProvider>();
        _services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        return this;
    }

    /// <summary>
    /// Adds the caching pipeline behavior with a custom cache provider.
    /// Use this when you want FusionCache, distributed cache, etc.
    /// </summary>
    /// <typeparam name="TCacheProvider">Your cache provider implementation.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddCachingPipeline<TCacheProvider>()
        where TCacheProvider : class, ICacheProvider
    {
        _services.TryAddSingleton<ICacheProvider, TCacheProvider>();
        _services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        return this;
    }

    /// <summary>
    /// Adds a custom pipeline behavior.
    /// </summary>
    /// <param name="behaviorType">The behavior type (must be open generic like typeof(MyBehavior&lt;,&gt;)).</param>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddBehavior(Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);
        _services.AddTransient(typeof(IPipelineBehavior<,>), behaviorType);
        return this;
    }

    /// <summary>
    /// Registers a streaming request handler as singleton (default for maximum performance).
    /// Automatically discovers TRequest and TResponse from the handler's implemented interface.
    /// Uses minimal reflection at startup only.
    /// </summary>
    /// <typeparam name="THandler">The streaming handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// IMPORTANT: Singleton handlers are cached and reused. Only use for handlers with no dependencies
    /// or only singleton dependencies. For handlers with scoped dependencies, use AddScopedStreamHandler.
    /// </remarks>
    [Obsolete("Use AddSingletonStreamHandler, AddScopedStreamHandler, or AddTransientStreamHandler to explicitly specify handler lifetime.")]
    public MediatorBuilder AddStreamHandler<THandler>()
        where THandler : class => AddStreamHandlerWithLifetime<THandler>(ServiceLifetime.Singleton);

    /// <summary>
    /// Registers a streaming request handler as transient.
    /// </summary>
    /// <typeparam name="THandler">The streaming handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddTransientStreamHandler<THandler>()
        where THandler : class => AddStreamHandlerWithLifetime<THandler>(ServiceLifetime.Transient);

    /// <summary>
    /// Registers a streaming request handler as scoped.
    /// Use for handlers with scoped dependencies.
    /// </summary>
    /// <typeparam name="THandler">The streaming handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddScopedStreamHandler<THandler>()
        where THandler : class => AddStreamHandlerWithLifetime<THandler>(ServiceLifetime.Scoped);

    /// <summary>
    /// Registers a streaming request handler as singleton.
    /// Use for handlers with no dependencies or only singleton dependencies.
    /// </summary>
    /// <typeparam name="THandler">The streaming handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddSingletonStreamHandler<THandler>()
        where THandler : class => AddStreamHandlerWithLifetime<THandler>(ServiceLifetime.Singleton);

    private MediatorBuilder AddStreamHandlerWithLifetime<THandler>(ServiceLifetime lifetime)
        where THandler : class
    {
        var handlerType = typeof(THandler);
        var handlerInterfaces = handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>))
            .ToArray();

        if (handlerInterfaces.Length == 0)
        {
            throw new InvalidOperationException(
                $"Handler type '{handlerType.Name}' must implement IStreamRequestHandler<TRequest, TResponse>.");
        }

        if (handlerInterfaces.Length > 1)
        {
            throw new InvalidOperationException(
                $"Handler type '{handlerType.Name}' implements multiple IStreamRequestHandler interfaces. " +
                $"Use the explicit AddStreamHandler<TRequest, TResponse, THandler>() overload to specify which interface to register.");
        }

        var handlerInterface = handlerInterfaces[0];
        _services.Add(new ServiceDescriptor(handlerInterface, handlerType, lifetime));
        HandlerLifetimeTracker.RegisterHandler(handlerInterface, lifetime);
        return this;
    }

    /// <summary>
    /// Registers a streaming request handler explicitly as singleton (default for maximum performance).
    /// Zero reflection.
    /// </summary>
    /// <typeparam name="TRequest">The streaming request type.</typeparam>
    /// <typeparam name="TResponse">The response item type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// IMPORTANT: Singleton handlers are cached and reused. Only use for handlers with no dependencies
    /// or only singleton dependencies. For handlers with scoped dependencies, use AddScopedStreamHandler.
    /// </remarks>
    [Obsolete("Use AddSingletonStreamHandler, AddScopedStreamHandler, or AddTransientStreamHandler to explicitly specify handler lifetime.")]
    public MediatorBuilder AddStreamHandler<TRequest, TResponse, THandler>()
        where TRequest : IStreamRequest<TResponse>
        where THandler : class, IStreamRequestHandler<TRequest, TResponse>
    {
        _services.AddSingleton<IStreamRequestHandler<TRequest, TResponse>, THandler>();
        HandlerLifetimeTracker.RegisterHandler(typeof(IStreamRequestHandler<TRequest, TResponse>), ServiceLifetime.Singleton);
        return this;
    }

    /// <summary>
    /// Registers a streaming request handler explicitly as transient.
    /// Zero reflection.
    /// </summary>
    /// <typeparam name="TRequest">The streaming request type.</typeparam>
    /// <typeparam name="TResponse">The response item type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddTransientStreamHandler<TRequest, TResponse, THandler>()
        where TRequest : IStreamRequest<TResponse>
        where THandler : class, IStreamRequestHandler<TRequest, TResponse>
    {
        _services.AddTransient<IStreamRequestHandler<TRequest, TResponse>, THandler>();
        HandlerLifetimeTracker.RegisterHandler(typeof(IStreamRequestHandler<TRequest, TResponse>), ServiceLifetime.Transient);
        return this;
    }

    /// <summary>
    /// Registers a streaming request handler explicitly as scoped.
    /// Zero reflection.
    /// </summary>
    /// <typeparam name="TRequest">The streaming request type.</typeparam>
    /// <typeparam name="TResponse">The response item type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddScopedStreamHandler<TRequest, TResponse, THandler>()
        where TRequest : IStreamRequest<TResponse>
        where THandler : class, IStreamRequestHandler<TRequest, TResponse>
    {
        _services.AddScoped<IStreamRequestHandler<TRequest, TResponse>, THandler>();
        HandlerLifetimeTracker.RegisterHandler(typeof(IStreamRequestHandler<TRequest, TResponse>), ServiceLifetime.Scoped);
        return this;
    }

    /// <summary>
    /// Registers a streaming request handler explicitly as singleton.
    /// Zero reflection.
    /// </summary>
    /// <typeparam name="TRequest">The streaming request type.</typeparam>
    /// <typeparam name="TResponse">The response item type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddSingletonStreamHandler<TRequest, TResponse, THandler>()
        where TRequest : IStreamRequest<TResponse>
        where THandler : class, IStreamRequestHandler<TRequest, TResponse>
    {
        _services.AddSingleton<IStreamRequestHandler<TRequest, TResponse>, THandler>();
        return this;
    }

    /// <summary>
    /// Adds a custom streaming pipeline behavior.
    /// </summary>
    /// <param name="behaviorType">The behavior type (must be open generic like typeof(MyStreamBehavior&lt;,&gt;)).</param>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddStreamBehavior(Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);
        _services.AddTransient(typeof(IStreamPipelineBehavior<,>), behaviorType);
        return this;
    }
}
