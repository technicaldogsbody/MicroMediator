
namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using BenchmarkDotNet.Attributes;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[ExcludeFromCodeCoverage]
[JsonExporterAttribute.Brief]
[AsciiDocExporter]
[KeepBenchmarkFiles]
public class PipelineBenchmarks
{
    private ServiceProvider _simpleMediatorProvider = null!;
    private ServiceProvider _mediatrProvider = null!;
    private IMediator _simpleMediator = null!;
    private MediatR.IMediator _mediatr = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup SimpleMediator with validation pipeline
        var simpleServices = new ServiceCollection();
        simpleServices.AddMemoryCache();
        simpleServices.AddSingleton(NullLoggerFactory.Instance);
        simpleServices.AddLogging();
        simpleServices
            .AddMediator()
            .AddSingletonHandler<ValidatedQueryHandler>()
            .AddValidator<ValidatedQueryValidator>()
            .AddDefaultLoggingPipeline();

        _simpleMediatorProvider = simpleServices.BuildServiceProvider();
        _simpleMediator = _simpleMediatorProvider.GetRequiredService<IMediator>();

        // Setup MediatR with validation pipeline
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMemoryCache();
        mediatrServices.AddSingleton(NullLoggerFactory.Instance);
        mediatrServices.AddLogging();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PipelineBenchmarks>());
        mediatrServices.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatrValidationBehavior<,>));
        mediatrServices.AddTransient<IValidator<MediatrValidatedQuery>, MediatrValidatedQueryValidator>();

        _mediatrProvider = mediatrServices.BuildServiceProvider();
        _mediatr = _mediatrProvider.GetRequiredService<MediatR.IMediator>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _simpleMediatorProvider?.Dispose();
        _mediatrProvider?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> SimpleMediator_WithValidation() => await _simpleMediator.SendAsync(new ValidatedQuery { Value = 42 });

    [Benchmark]
    public async Task<int> MediatR_WithValidation() => await _mediatr.Send(new MediatrValidatedQuery { Value = 42 });

    // SimpleMediator types
    public record ValidatedQuery : Abstractions.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class ValidatedQueryHandler : Abstractions.IRequestHandler<ValidatedQuery, int>
    {
        public ValueTask<int> HandleAsync(ValidatedQuery request, CancellationToken cancellationToken) => ValueTask.FromResult(request.Value * 2);
    }

    public class ValidatedQueryValidator : AbstractValidator<ValidatedQuery>
    {
        public ValidatedQueryValidator()
        {
            RuleFor(x => x.Value).GreaterThan(0);
        }
    }

    // MediatR types
    public record MediatrValidatedQuery : MediatR.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class MediatrValidatedQueryHandler : MediatR.IRequestHandler<MediatrValidatedQuery, int>
    {
        public Task<int> Handle(MediatrValidatedQuery request, CancellationToken cancellationToken) => Task.FromResult(request.Value * 2);
    }

    public class MediatrValidatedQueryValidator : AbstractValidator<MediatrValidatedQuery>
    {
        public MediatrValidatedQueryValidator()
        {
            RuleFor(x => x.Value).GreaterThan(0);
        }
    }

    // MediatR validation behavior (mimicking yours)
    public class MediatrValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
        : MediatR.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var validators1 = validators.ToArray();

            if (validators1.Length == 0)
            {
                return await next(cancellationToken);
            }

            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                validators1.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count > 0)
            {
                throw new ValidationException(failures);
            }

            return await next(cancellationToken);
        }
    }
}
