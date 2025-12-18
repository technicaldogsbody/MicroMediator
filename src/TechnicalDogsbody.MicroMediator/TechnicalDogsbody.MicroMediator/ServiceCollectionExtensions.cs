using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TechnicalDogsbody.MicroMediator.Abstractions;
using TechnicalDogsbody.MicroMediator.Behaviors;

namespace TechnicalDogsbody.MicroMediator;

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
    /// Registers a request handler by handler type.
    /// Automatically discovers TRequest and TResponse from the handler's implemented interface.
    /// Uses minimal reflection at startup only.
    /// </summary>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddHandler<THandler>()
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

        _services.AddTransient(handlerInterfaces[0], handlerType);
        return this;
    }

    /// <summary>
    /// Registers a request handler explicitly with all type parameters.
    /// Zero reflection.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddHandler<TRequest, TResponse, THandler>()
        where TRequest : IRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        _services.AddTransient<IRequestHandler<TRequest, TResponse>, THandler>();
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
    /// Adds the default caching pipeline behavior.
    /// Ensures IMemoryCache is registered.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public MediatorBuilder AddDefaultCachingPipeline()
    {
        _services.TryAddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions()));
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
}
