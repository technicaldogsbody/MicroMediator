
namespace TechnicalDogsbody.MicroMediator.Tests;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

public class MediatorTests
{
    [Fact]
    public async Task SendAsync_WithValidRequest_CallsHandler()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<TestRequest, string, TestRequestHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.SendAsync(new TestRequest { Value = "test" });

        Assert.Equal("Handled: test", result);
    }

    [Fact]
    public async Task SendAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        services.AddMediator();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            mediator.SendAsync<string>(null!).AsTask());
    }

    [Fact]
    public async Task SendAsync_WithoutRegisteredHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddMediator();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mediator.SendAsync(new TestRequest { Value = "test" }).AsTask());

        Assert.Contains("No handler registered", exception.Message);
        Assert.Contains("TestRequest", exception.Message);
    }

    [Fact]
    public async Task SendAsync_WithMultipleCalls_CachesHandlerResolution()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<TestRequest, string, TestRequestHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        string result1 = await mediator.SendAsync(new TestRequest { Value = "test1" });
        string result2 = await mediator.SendAsync(new TestRequest { Value = "test2" });

        Assert.Equal("Handled: test1", result1);
        Assert.Equal("Handled: test2", result2);
    }

    [Fact]
    public void Constructor_WithNullProvider_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(() => new Mediator(null!));

    [ExcludeFromCodeCoverage]
    private record TestRequest : IRequest<string>
    {
        public required string Value { get; init; }
    }

    [ExcludeFromCodeCoverage]
    private class TestRequestHandler : IRequestHandler<TestRequest, string>
    {
        public ValueTask<string> HandleAsync(TestRequest request, CancellationToken cancellationToken) => ValueTask.FromResult($"Handled: {request.Value}");
    }
}
