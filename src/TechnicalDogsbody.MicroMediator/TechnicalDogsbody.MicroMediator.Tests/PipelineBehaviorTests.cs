
namespace TechnicalDogsbody.MicroMediator.Tests;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

public class PipelineBehaviorTests
{
    [Fact]
    public async Task SendAsync_WithNoBehaviors_CallsHandlerDirectly()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<SimpleRequest, string, SimpleRequestHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.SendAsync(new SimpleRequest());

        Assert.Equal("Simple", result);
    }

    [Fact]
    public async Task SendAsync_WithSingleBehavior_ExecutesBehavior()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<TestBehaviorRequest, string, TestBehaviorRequestHandler>()
            .AddBehavior(typeof(TestBehavior<,>));

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.SendAsync(new TestBehaviorRequest());

        Assert.Equal("[Before]Test[After]", result);
    }

    [Fact]
    public async Task SendAsync_WithMultipleBehaviors_ExecutesInCorrectOrder()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<MultiBehaviorRequest, string, MultiBehaviorRequestHandler>()
            .AddBehavior(typeof(FirstBehavior<,>))
            .AddBehavior(typeof(SecondBehavior<,>));

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.SendAsync(new MultiBehaviorRequest());

        // Behaviors execute in reverse registration order (last registered runs first)
        Assert.Equal("[Second][First]Multi[/First][/Second]", result);
    }

    [Fact]
    public async Task SendAsync_BehaviorThrowsException_PropagatesException()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<ExceptionRequest, string, ExceptionRequestHandler>()
            .AddBehavior(typeof(ExceptionBehavior<,>));

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mediator.SendAsync(new ExceptionRequest()).AsTask());
    }

    [Fact]
    public async Task SendAsync_BehaviorShortCircuits_DoesNotCallHandler()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<ShortCircuitRequest, string, ShortCircuitRequestHandler>()
            .AddBehavior(typeof(ShortCircuitBehavior<,>));

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.SendAsync(new ShortCircuitRequest());

        Assert.Equal("ShortCircuit", result);
    }

    [ExcludeFromCodeCoverage]
    private record SimpleRequest : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private class SimpleRequestHandler : IRequestHandler<SimpleRequest, string>
    {
        public ValueTask<string> HandleAsync(SimpleRequest request, CancellationToken cancellationToken) => ValueTask.FromResult("Simple");
    }

    [ExcludeFromCodeCoverage]
    private record TestBehaviorRequest : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private class TestBehaviorRequestHandler : IRequestHandler<TestBehaviorRequest, string>
    {
        public ValueTask<string> HandleAsync(TestBehaviorRequest request, CancellationToken cancellationToken) => ValueTask.FromResult("Test");
    }

    [ExcludeFromCodeCoverage]
    private record MultiBehaviorRequest : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private class MultiBehaviorRequestHandler : IRequestHandler<MultiBehaviorRequest, string>
    {
        public ValueTask<string> HandleAsync(MultiBehaviorRequest request, CancellationToken cancellationToken) => ValueTask.FromResult("Multi");
    }

    [ExcludeFromCodeCoverage]
    private record ExceptionRequest : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private class ExceptionRequestHandler : IRequestHandler<ExceptionRequest, string>
    {
        public ValueTask<string> HandleAsync(ExceptionRequest request, CancellationToken cancellationToken) => ValueTask.FromResult("Exception");
    }

    [ExcludeFromCodeCoverage]
    private record ShortCircuitRequest : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private class ShortCircuitRequestHandler : IRequestHandler<ShortCircuitRequest, string>
    {
        public ValueTask<string> HandleAsync(ShortCircuitRequest request, CancellationToken cancellationToken) => ValueTask.FromResult("ShouldNotBeCalled");
    }

    [ExcludeFromCodeCoverage]
    private class TestBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var result = await next();
            return (TResponse)(object)$"[Before]{result}[After]";
        }
    }

    [ExcludeFromCodeCoverage]
    private class FirstBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var result = await next();
            return (TResponse)(object)$"[First]{result}[/First]";
        }
    }

    [ExcludeFromCodeCoverage]
    private class SecondBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var result = await next();
            return (TResponse)(object)$"[Second]{result}[/Second]";
        }
    }

    [ExcludeFromCodeCoverage]
    private class ExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => throw new InvalidOperationException("Behavior exception");
    }

    [ExcludeFromCodeCoverage]
    private class ShortCircuitBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken) => ValueTask.FromResult((TResponse)(object)"ShortCircuit");
    }
}
