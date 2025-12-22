namespace TechnicalDogsbody.MicroMediator.Tests;

using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMediator_RegistersIMediator()
    {
        var services = new ServiceCollection();

        services.AddMediator();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetService<IMediator>();

        Assert.NotNull(mediator);
    }

    [Fact]
    public void AddMediator_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(services.AddMediator);
    }

    [Fact]
    public void MediatorBuilder_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        
        var exception = Assert.Throws<ArgumentNullException>(() => new MediatorBuilder(services));
        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddHandler_GenericInference_RegistersHandler()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddHandler<TestHandler>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<IRequestHandler<TestRequest, string>>();

        Assert.NotNull(handler);
    }

    [Fact]
    public void AddHandler_ExplicitTypes_RegistersHandler()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddHandler<TestRequest, string, TestHandler>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<IRequestHandler<TestRequest, string>>();

        Assert.NotNull(handler);
    }

    [Fact]
    public void AddHandler_NonHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator().AddHandler<NotAHandler>());

        Assert.Contains("must implement IRequestHandler", exception.Message);
    }

    [Fact]
    public void AddHandler_MultipleInterfaces_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator().AddHandler<MultipleInterfaceHandler>());

        Assert.Contains("implements multiple IRequestHandler interfaces", exception.Message);
    }

    [Fact]
    public void AddHandler_WithNonGenericInterfaces_IgnoresThemAndRegistersCorrectly()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddHandler<HandlerWithNonGenericInterface>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<IRequestHandler<TestRequest, string>>();

        Assert.NotNull(handler);
    }

    [Fact]
    public void AddValidator_GenericInference_RegistersValidator()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddValidator<TestValidator>();

        var provider = services.BuildServiceProvider();
        var validator = provider.GetService<IValidator<TestRequest>>();

        Assert.NotNull(validator);
    }

    [Fact]
    public void AddValidator_ExplicitTypes_RegistersValidator()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddValidator<TestRequest, TestValidator>();

        var provider = services.BuildServiceProvider();
        var validator = provider.GetService<IValidator<TestRequest>>();

        Assert.NotNull(validator);
    }

    [Fact]
    public void AddValidator_FirstValidator_AddsValidationBehavior()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddValidator<TestValidator>();

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices<IPipelineBehavior<TestRequest, string>>();

        Assert.Single(behaviors);
    }

    [Fact]
    public void AddValidator_SecondValidator_DoesNotAddDuplicateBehavior()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddValidator<TestValidator>()
            .AddValidator<SecondTestValidator>();

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices<IPipelineBehavior<TestRequest, string>>();

        Assert.Single(behaviors);
    }

    [Fact]
    public void AddValidator_NonValidator_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator().AddValidator<NotAValidator>());

        Assert.Contains("must implement IValidator", exception.Message);
    }

    [Fact]
    public void AddValidator_MultipleInterfaces_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator().AddValidator<MultipleInterfaceValidator>());

        Assert.Contains("implements multiple IValidator interfaces", exception.Message);
    }

    [Fact]
    public void AddDefaultLoggingPipeline_RegistersLoggingBehavior()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMediator()
            .AddDefaultLoggingPipeline();

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices<IPipelineBehavior<TestRequest, string>>();

        Assert.Single(behaviors);
    }

    [Fact]
    public void AddDefaultCachingPipeline_RegistersCachingBehaviorAndMemoryCache()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddDefaultCachingPipeline();

        var provider = services.BuildServiceProvider();
        var cache = provider.GetService<IMemoryCache>();
        var cacheProvider = provider.GetService<ICacheProvider>();
        var behaviors = provider.GetServices<IPipelineBehavior<TestRequest, string>>();

        Assert.NotNull(cache);
        Assert.NotNull(cacheProvider);
        Assert.Single(behaviors);
    }

    [Fact]
    public void AddCachingPipeline_WithCustomProvider_RegistersCustomProvider()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddCachingPipeline<TestCacheProvider>();

        var provider = services.BuildServiceProvider();
        var cacheProvider = provider.GetService<ICacheProvider>();
        var behaviors = provider.GetServices<IPipelineBehavior<TestRequest, string>>();

        Assert.NotNull(cacheProvider);
        Assert.IsType<TestCacheProvider>(cacheProvider);
        Assert.Single(behaviors);
    }

    [Fact]
    public void AddCachingPipeline_DoesNotRegisterMemoryCache()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddCachingPipeline<TestCacheProvider>();

        var provider = services.BuildServiceProvider();
        var cache = provider.GetService<IMemoryCache>();

        Assert.Null(cache);
    }

    [Fact]
    public void AddBehavior_WithValidType_RegistersBehavior()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddBehavior(typeof(CustomBehavior<,>));

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices<IPipelineBehavior<TestRequest, string>>();

        Assert.Single(behaviors);
    }

    [Fact]
    public void AddBehavior_WithNullType_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddMediator().AddBehavior(null!));
    }

    [ExcludeFromCodeCoverage]
    private record TestRequest : IRequest<string>
    {
        public string Value { get; init; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    private record SecondRequest : IRequest<int>;

    [ExcludeFromCodeCoverage]
    private class TestHandler : IRequestHandler<TestRequest, string>
    {
        public ValueTask<string> HandleAsync(TestRequest request, CancellationToken cancellationToken) => ValueTask.FromResult("test");
    }

    [ExcludeFromCodeCoverage]
    private class MultipleInterfaceHandler :
        IRequestHandler<TestRequest, string>,
        IRequestHandler<SecondRequest, int>
    {
        public ValueTask<string> HandleAsync(TestRequest request, CancellationToken cancellationToken) => ValueTask.FromResult("test");

        public ValueTask<int> HandleAsync(SecondRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(42);
    }

    [ExcludeFromCodeCoverage]
    private class NotAHandler { }

    [ExcludeFromCodeCoverage]
    private class HandlerWithNonGenericInterface : IRequestHandler<TestRequest, string>, IDisposable
    {
        public ValueTask<string> HandleAsync(TestRequest request, CancellationToken cancellationToken) => ValueTask.FromResult("test");

        public void Dispose() { }
    }

    [ExcludeFromCodeCoverage]
    private class TestValidator : AbstractValidator<TestRequest>
    {
        public TestValidator()
        {
            RuleFor(x => x.Value).NotEmpty();
        }
    }

    [ExcludeFromCodeCoverage]
    private class SecondTestValidator : AbstractValidator<TestRequest>
    {
        public SecondTestValidator()
        {
            RuleFor(x => x.Value).MinimumLength(5);
        }
    }

    [ExcludeFromCodeCoverage]
    private class MultipleInterfaceValidator :
        AbstractValidator<TestRequest>,
        IValidator<SecondRequest>
    {
        public MultipleInterfaceValidator()
        {
            RuleFor(x => x.Value).NotEmpty();
        }

        ValidationResult IValidator<SecondRequest>.Validate(SecondRequest instance) => new();

        Task<ValidationResult> IValidator<SecondRequest>.ValidateAsync(SecondRequest instance, CancellationToken cancellation) => Task.FromResult(new ValidationResult());

        IValidatorDescriptor IValidator.CreateDescriptor() => new ValidatorDescriptor<SecondRequest>([]);

        bool IValidator.CanValidateInstancesOfType(Type type) => type == typeof(TestRequest) || type == typeof(SecondRequest);
    }

    [ExcludeFromCodeCoverage]
    private class NotAValidator { }

    [ExcludeFromCodeCoverage]
    private class CustomBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => await next();
    }

    [ExcludeFromCodeCoverage]
    private class TestCacheProvider : ICacheProvider
    {
        private readonly Dictionary<string, object> _cache = new();

        public bool TryGet<TResponse>(string cacheKey, out TResponse? value)
        {
            if (_cache.TryGetValue(cacheKey, out object? obj))
            {
                value = (TResponse)obj;
                return true;
            }

            value = default;
            return false;
        }

        public void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration) => _cache[cacheKey] = value!;
    }
}
