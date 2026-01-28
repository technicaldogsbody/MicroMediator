
#pragma warning disable CA1822
namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 0, iterationCount: 1, invocationCount: 1)]
[ExcludeFromCodeCoverage]
[JsonExporterAttribute.Brief]
[AsciiDocExporter]
[KeepBenchmarkFiles]
public class ColdStartBenchmarks
{
    [Benchmark(Baseline = true)]
    public async Task<int> SimpleMediator_FirstRequest()
    {
        // Setup and execute first request (cold start)
        var services = new ServiceCollection();
        services
            .AddMediator()
            .AddSingletonHandler<ColdStartQuery, int, ColdStartQueryHandler>();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        return await mediator.SendAsync(new ColdStartQuery { Value = 42 });
    }

    [Benchmark]
    public async Task<int> MediatR_FirstRequest()
    {
        // Setup and execute first request (cold start)
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ColdStartBenchmarks>());

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<MediatR.IMediator>();

        return await mediator.Send(new MediatrColdStartQuery { Value = 42 });
    }

    // SimpleMediator types
    public record ColdStartQuery : Abstractions.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class ColdStartQueryHandler : Abstractions.IRequestHandler<ColdStartQuery, int>
    {
        public ValueTask<int> HandleAsync(ColdStartQuery request, CancellationToken cancellationToken) => ValueTask.FromResult(request.Value * 2);
    }

    // MediatR types
    public record MediatrColdStartQuery : MediatR.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class MediatrColdStartQueryHandler : MediatR.IRequestHandler<MediatrColdStartQuery, int>
    {
        public Task<int> Handle(MediatrColdStartQuery request, CancellationToken cancellationToken) => Task.FromResult(request.Value * 2);
    }
}
