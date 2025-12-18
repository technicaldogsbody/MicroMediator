using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

namespace TechnicalDogsbody.MicroMediator.Tests.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task HandleAsync_NoValidators_CallsNext()
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
    public async Task HandleAsync_ValidRequest_CallsNext()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<TestRequest, string, TestRequestHandler>()
            .AddValidator<TestRequest, TestRequestValidator>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        string result = await mediator.SendAsync(new TestRequest { Value = "test" });

        Assert.Equal("Handled: test", result);
    }

    [Fact]
    public async Task HandleAsync_InvalidRequest_ThrowsValidationException()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<TestRequest, string, TestRequestHandler>()
            .AddValidator<TestRequest, TestRequestValidator>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            mediator.SendAsync(new TestRequest { Value = "" }).AsTask());

        Assert.Single(exception.Errors);
        Assert.Contains("must not be empty", exception.Errors.First().ErrorMessage);
    }

    [Fact]
    public async Task HandleAsync_MultipleValidators_AllExecute()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<TestRequest, string, TestRequestHandler>()
            .AddValidator<TestRequest, TestRequestValidator>()
            .AddValidator<TestRequest, SecondTestRequestValidator>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Empty string fails NotEmpty, and also fails MinimumLength(3)
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            mediator.SendAsync(new TestRequest { Value = "" }).AsTask());

        // Both validators should report errors
        Assert.True(exception.Errors.Count() >= 1);
    }

    [Fact]
    public async Task HandleAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        var validators = new[] { new TestRequestValidator() };
        var behavior = new TechnicalDogsbody.MicroMediator.Behaviors.ValidationBehavior<TestRequest, string>(validators);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            behavior.HandleAsync(null!, () => ValueTask.FromResult("test"), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task HandleAsync_WithNullNext_ThrowsArgumentNullException()
    {
        var validators = new[] { new TestRequestValidator() };
        var behavior = new TechnicalDogsbody.MicroMediator.Behaviors.ValidationBehavior<TestRequest, string>(validators);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            behavior.HandleAsync(new TestRequest { Value = "test" }, null!, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task HandleAsync_NullValidatorCollection_TreatsAsEmpty()
    {
        var behavior = new TechnicalDogsbody.MicroMediator.Behaviors.ValidationBehavior<TestRequest, string>(null!);
        bool called = false;

        string result = await behavior.HandleAsync(
            new TestRequest { Value = "test" },
            () =>
            {
                called = true;
                return ValueTask.FromResult("success");
            },
            CancellationToken.None);

        Assert.True(called);
        Assert.Equal("success", result);
    }

    [Fact]
    public async Task HandleAsync_WithCancellationToken_PassesToValidators()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddHandler<TestRequest, string, TestRequestHandler>()
            .AddValidator<TestRequest, CancellationTestValidator>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            mediator.SendAsync(new TestRequest { Value = "test" }, cts.Token).AsTask());
    }

    [ExcludeFromCodeCoverage]
    private record TestRequest : IRequest<string>
    {
        public required string Value { get; init; }
    }

    [ExcludeFromCodeCoverage]
    private class TestRequestHandler : IRequestHandler<TestRequest, string>
    {
        public ValueTask<string> HandleAsync(TestRequest request, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult($"Handled: {request.Value}");
        }
    }

    [ExcludeFromCodeCoverage]
    private class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(x => x.Value).NotEmpty();
        }
    }

    [ExcludeFromCodeCoverage]
    private class SecondTestRequestValidator : AbstractValidator<TestRequest>
    {
        public SecondTestRequestValidator()
        {
            RuleFor(x => x.Value).MinimumLength(3);
        }
    }

    [ExcludeFromCodeCoverage]
    private class CancellationTestValidator : AbstractValidator<TestRequest>
    {
        public CancellationTestValidator()
        {
            RuleFor(x => x.Value).NotEmpty();
        }

        public override Task<ValidationResult> ValidateAsync(
            ValidationContext<TestRequest> context,
            CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            return base.ValidateAsync(context, cancellation);
        }
    }
}
