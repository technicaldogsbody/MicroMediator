
#pragma warning disable CA1822
namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using BenchmarkDotNet.Attributes;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

[MemoryDiagnoser]
[ExcludeFromCodeCoverage]
[JsonExporterAttribute.Brief]
[AsciiDocExporter]
[KeepBenchmarkFiles]
public class RegistrationBenchmarks
{
    private ServiceProvider _explicitProvider = null!;
    private ServiceProvider _reflectionProvider = null!;
    private IMediator _explicitMediator = null!;
    private IMediator _reflectionMediator = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup with explicit registration
        var explicitServices = new ServiceCollection();
        explicitServices
            .AddMediator()
            .AddSingletonHandler<RegistrationQuery, int, RegistrationQueryHandler>()
            .AddValidator<RegistrationQuery, RegistrationQueryValidator>();
        _explicitProvider = explicitServices.BuildServiceProvider();
        _explicitMediator = _explicitProvider.GetRequiredService<IMediator>();

        // Setup with reflection-based registration
        var reflectionServices = new ServiceCollection();
        reflectionServices
            .AddMediator()
            .AddSingletonHandler<RegistrationQueryHandler>()
            .AddValidator<RegistrationQueryValidator>();
        _reflectionProvider = reflectionServices.BuildServiceProvider();
        _reflectionMediator = _reflectionProvider.GetRequiredService<IMediator>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _explicitProvider?.Dispose();
        _reflectionProvider?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task<int> Explicit_Registration_Send() => await _explicitMediator.SendAsync(new RegistrationQuery { Value = 42 });

    [Benchmark]
    public async Task<int> Reflection_Registration_Send() => await _reflectionMediator.SendAsync(new RegistrationQuery { Value = 42 });

    [Benchmark]
    public async Task<int> Explicit_Registration_ColdStart()
    {
        // Setup and execute first request (cold start)
        var services = new ServiceCollection();
        services
            .AddMediator()
            .AddSingletonHandler<RegistrationQuery, int, RegistrationQueryHandler>()
            .AddValidator<RegistrationQuery, RegistrationQueryValidator>();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        return await mediator.SendAsync(new RegistrationQuery { Value = 42 });
    }

    [Benchmark]
    public async Task<int> Reflection_Registration_ColdStart()
    {
        // Setup and execute first request (cold start)
        var services = new ServiceCollection();
        services
            .AddMediator()
            .AddSingletonHandler<RegistrationQueryHandler>()
            .AddValidator<RegistrationQueryValidator>();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        return await mediator.SendAsync(new RegistrationQuery { Value = 42 });
    }

    // Shared types
    public record RegistrationQuery : Abstractions.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class RegistrationQueryHandler : Abstractions.IRequestHandler<RegistrationQuery, int>
    {
        public ValueTask<int> HandleAsync(RegistrationQuery request, CancellationToken cancellationToken) => ValueTask.FromResult(request.Value * 2);
    }

    public class RegistrationQueryValidator : AbstractValidator<RegistrationQuery>
    {
        public RegistrationQueryValidator()
        {
            RuleFor(x => x.Value).GreaterThan(0);
        }
    }
}
