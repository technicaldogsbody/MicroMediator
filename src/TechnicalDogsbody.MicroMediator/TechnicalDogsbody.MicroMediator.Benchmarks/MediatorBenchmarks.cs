
namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[ExcludeFromCodeCoverage]
public class MediatorBenchmarks
{
    private ServiceProvider _simpleMediatorProvider = null!;
    private ServiceProvider _mediatrProvider = null!;
    private IMediator _simpleMediator = null!;
    private MediatR.IMediator _mediatr = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup SimpleMediator
        var simpleServices = new ServiceCollection();
        simpleServices
            .AddMediator()
            .AddHandler<SimpleQueryHandler>();
        _simpleMediatorProvider = simpleServices.BuildServiceProvider();
        _simpleMediator = _simpleMediatorProvider.GetRequiredService<IMediator>();

        // Setup MediatR
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatorBenchmarks>());
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
    public async Task<int> SimpleMediator_Send() => await _simpleMediator.SendAsync(new SimpleQuery { Value = 42 });

    [Benchmark]
    public async Task<int> MediatR_Send() => await _mediatr.Send(new MediatrQuery { Value = 42 });

    // SimpleMediator types
    public record SimpleQuery : Abstractions.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class SimpleQueryHandler : Abstractions.IRequestHandler<SimpleQuery, int>
    {
        public ValueTask<int> HandleAsync(SimpleQuery request, CancellationToken cancellationToken) => ValueTask.FromResult(request.Value * 2);
    }

    // MediatR types
    public record MediatrQuery : MediatR.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class MediatrQueryHandler : MediatR.IRequestHandler<MediatrQuery, int>
    {
        public Task<int> Handle(MediatrQuery request, CancellationToken cancellationToken) => Task.FromResult(request.Value * 2);
    }
}
