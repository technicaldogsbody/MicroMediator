using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

namespace TechnicalDogsbody.MicroMediator.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
[ExcludeFromCodeCoverage]
public class ThroughputBenchmarks
{
    private ServiceProvider _simpleMediatorProvider = null!;
    private ServiceProvider _mediatrProvider = null!;
    private IMediator _simpleMediator = null!;
    private MediatR.IMediator _mediatr = null!;

    [Params(100, 1000, 10000)]
    public int RequestCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Setup SimpleMediator
        var simpleServices = new ServiceCollection();
        simpleServices
            .AddMediator()
            .AddHandler<ThroughputQuery, int, ThroughputQueryHandler>();
        _simpleMediatorProvider = simpleServices.BuildServiceProvider();
        _simpleMediator = _simpleMediatorProvider.GetRequiredService<IMediator>();

        // Setup MediatR
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ThroughputBenchmarks>());
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
    public async Task<int> SimpleMediator_Sequential()
    {
        int total = 0;
        for (int i = 0; i < RequestCount; i++)
        {
            total += await _simpleMediator.SendAsync(new ThroughputQuery { Value = i });
        }

        return total;
    }

    [Benchmark]
    public async Task<int> MediatR_Sequential()
    {
        int total = 0;
        for (int i = 0; i < RequestCount; i++)
        {
            total += await _mediatr.Send(new MediatrThroughputQuery { Value = i });
        }

        return total;
    }

    [Benchmark]
    public async Task<int> SimpleMediator_Parallel()
    {
        var tasks = new Task<int>[RequestCount];
        for (int i = 0; i < RequestCount; i++)
        {
            int value = i;
            tasks[i] = _simpleMediator.SendAsync(new ThroughputQuery { Value = value }).AsTask();
        }

        int[] results = await Task.WhenAll(tasks);
        return results.Sum();
    }

    [Benchmark]
    public async Task<int> MediatR_Parallel()
    {
        var tasks = new Task<int>[RequestCount];
        for (int i = 0; i < RequestCount; i++)
        {
            int value = i;
            tasks[i] = _mediatr.Send(new MediatrThroughputQuery { Value = value });
        }

        int[] results = await Task.WhenAll(tasks);
        return results.Sum();
    }

    // SimpleMediator types
    public record ThroughputQuery : Abstractions.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class ThroughputQueryHandler : Abstractions.IRequestHandler<ThroughputQuery, int>
    {
        public ValueTask<int> HandleAsync(ThroughputQuery request, CancellationToken cancellationToken)
        {
            // Simulate minimal work
            return ValueTask.FromResult(request.Value * 2);
        }
    }

    // MediatR types
    public record MediatrThroughputQuery : MediatR.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class MediatrThroughputQueryHandler : MediatR.IRequestHandler<MediatrThroughputQuery, int>
    {
        public Task<int> Handle(MediatrThroughputQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Value * 2);
        }
    }
}
