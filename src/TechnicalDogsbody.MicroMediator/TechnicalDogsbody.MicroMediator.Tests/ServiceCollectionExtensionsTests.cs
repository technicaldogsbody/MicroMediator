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
    public void AddSingletonHandler_GenericInference_RegistersHandler()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddSingletonHandler<TestHandler>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<IRequestHandler<TestRequest, string>>();

        Assert.NotNull(handler);
    }

    [Fact]
    public void AddSingletonHandler_ExplicitTypes_RegistersHandler()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddSingletonHandler<TestRequest, string, TestHandler>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<IRequestHandler<TestRequest, string>>();

        Assert.NotNull(handler);
    }

    [Fact]
    public void AddSingletonHandler_NonHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator().AddSingletonHandler<NotAHandler>());

        Assert.Contains("must implement IRequestHandler", exception.Message);
    }

    [Fact]
    public void AddSingletonHandler_MultipleInterfaces_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator().AddSingletonHandler<MultipleInterfaceHandler>());

        Assert.Contains("implements multiple IRequestHandler interfaces", exception.Message);
    }

    [Fact]
    public void AddSingletonHandler_WithNonGenericInterfaces_IgnoresThemAndRegistersCorrectly()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddSingletonHandler<HandlerWithNonGenericInterface>();

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

    [Fact]
    public void AddSingletonStreamHandler_GenericInference_RegistersHandler()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddSingletonStreamHandler<TestStreamHandler>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<IStreamRequestHandler<TestStreamRequest, int>>();

        Assert.NotNull(handler);
    }

    [Fact]
    public void AddSingletonStreamHandler_ExplicitTypes_RegistersHandler()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddSingletonStreamHandler<TestStreamRequest, int, TestStreamHandler>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<IStreamRequestHandler<TestStreamRequest, int>>();

        Assert.NotNull(handler);
    }

    [Fact]
    public void AddSingletonStreamHandler_NonHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator().AddSingletonStreamHandler<NotAHandler>());

        Assert.Contains("must implement IStreamRequestHandler", exception.Message);
    }

    [Fact]
    public void AddSingletonStreamHandler_MultipleInterfaces_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator().AddSingletonStreamHandler<MultipleStreamInterfaceHandler>());

        Assert.Contains("implements multiple IStreamRequestHandler interfaces", exception.Message);
    }

    [Fact]
    public void AddSingletonStreamHandler_WithNonGenericInterfaces_IgnoresThemAndRegistersCorrectly()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddSingletonStreamHandler<StreamHandlerWithNonGenericInterface>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetService<IStreamRequestHandler<TestStreamRequest, int>>();

        Assert.NotNull(handler);
    }

    [Fact]
    public void AddStreamBehavior_WithValidType_RegistersBehavior()
    {
        var services = new ServiceCollection();

        services.AddMediator()
            .AddStreamBehavior(typeof(CustomStreamBehavior<,>));

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices<IStreamPipelineBehavior<TestStreamRequest, int>>();

        Assert.Single(behaviors);
    }

    [Fact]
    public void AddStreamBehavior_WithNullType_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddMediator().AddStreamBehavior(null!));
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
        private readonly Dictionary<string, object> _cache = [];

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

    // Streaming test types
    [ExcludeFromCodeCoverage]
    private record TestStreamRequest : IStreamRequest<int>
    {
        public int Count { get; init; }
    }

    [ExcludeFromCodeCoverage]
    private record SecondStreamRequest : IStreamRequest<string>;

    [ExcludeFromCodeCoverage]
    private class TestStreamHandler : IStreamRequestHandler<TestStreamRequest, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TestStreamRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 1; i <= request.Count; i++)
            {
                await Task.Yield();
                yield return i;
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private class MultipleStreamInterfaceHandler :
        IStreamRequestHandler<TestStreamRequest, int>,
        IStreamRequestHandler<SecondStreamRequest, string>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TestStreamRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return 1;
        }

        public async IAsyncEnumerable<string> HandleAsync(
            SecondStreamRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return "test";
        }
    }

    [ExcludeFromCodeCoverage]
    private class StreamHandlerWithNonGenericInterface : IStreamRequestHandler<TestStreamRequest, int>, IDisposable
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TestStreamRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return 1;
        }

        public void Dispose() { }
    }

    [ExcludeFromCodeCoverage]
    private class CustomStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        public async IAsyncEnumerable<TResponse> HandleAsync(
            TRequest request,
            StreamHandlerDelegate<TResponse> next,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in next().WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
    }
}
