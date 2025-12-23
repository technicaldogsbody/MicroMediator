
namespace TechnicalDogsbody.MicroMediator.Tests.Behaviors;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TechnicalDogsbody.MicroMediator.Abstractions;

public class LoggingBehaviorTests
{
    [Fact]
    public async Task HandleAsync_SuccessfulExecution_LogsStartAndEnd()
    {
        var logger = new TestLogger<TechnicalDogsbody.MicroMediator.Behaviors.LoggingBehavior<TestRequest, string>>();
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<TechnicalDogsbody.MicroMediator.Behaviors.LoggingBehavior<TestRequest, string>>>(logger);
        services.AddMediator()
            .AddHandler<TestRequest, string, TestRequestHandler>()
            .AddDefaultLoggingPipeline();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.SendAsync(new TestRequest { Value = "test" });

        Assert.Equal("Handled: test", result);
        Assert.Equal(2, logger.LogCount);
        Assert.Contains("Handling TestRequest", logger.Messages[0]);
        Assert.Contains("Handled TestRequest", logger.Messages[1]);
    }

    [Fact]
    public async Task HandleAsync_ExceptionThrown_LogsError()
    {
        var logger = new TestLogger<TechnicalDogsbody.MicroMediator.Behaviors.LoggingBehavior<ThrowingRequest, string>>();
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<TechnicalDogsbody.MicroMediator.Behaviors.LoggingBehavior<ThrowingRequest, string>>>(logger);
        services.AddMediator()
            .AddHandler<ThrowingRequest, string, ThrowingRequestHandler>()
            .AddDefaultLoggingPipeline();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mediator.SendAsync(new ThrowingRequest()).AsTask());

        Assert.Equal(2, logger.LogCount);
        Assert.Contains("Handling ThrowingRequest", logger.Messages[0]);
        Assert.Contains("Error handling ThrowingRequest", logger.Messages[1]);
    }

    [Fact]
    public async Task HandleAsync_TracksElapsedTime()
    {
        var logger = new TestLogger<TechnicalDogsbody.MicroMediator.Behaviors.LoggingBehavior<SlowRequest, string>>();
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<TechnicalDogsbody.MicroMediator.Behaviors.LoggingBehavior<SlowRequest, string>>>(logger);
        services.AddMediator()
            .AddHandler<SlowRequest, string, SlowRequestHandler>()
            .AddDefaultLoggingPipeline();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.SendAsync(new SlowRequest());

        Assert.Equal(2, logger.LogCount);
        Assert.Contains("ms", logger.Messages[1]);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TechnicalDogsbody.MicroMediator.Behaviors.LoggingBehavior<TestRequest, string>(null!));
    }

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

    [ExcludeFromCodeCoverage]
    private record ThrowingRequest : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private class ThrowingRequestHandler : IRequestHandler<ThrowingRequest, string>
    {
        public ValueTask<string> HandleAsync(ThrowingRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("Handler exception");
    }

    [ExcludeFromCodeCoverage]
    private record SlowRequest : IRequest<string>;

    [ExcludeFromCodeCoverage]
    private class SlowRequestHandler : IRequestHandler<SlowRequest, string>
    {
        public async ValueTask<string> HandleAsync(SlowRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken);
            return "Slow";
        }
    }

    [ExcludeFromCodeCoverage]
    private class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public int LogCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogCount++;
            string message = formatter(state, exception);
            Messages.Add(message);
        }
    }
}
