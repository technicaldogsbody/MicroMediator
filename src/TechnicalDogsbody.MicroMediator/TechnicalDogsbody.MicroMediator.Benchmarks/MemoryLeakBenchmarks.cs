namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

/// <summary>
/// Tests for memory leaks over extended operation.
/// Verifies that allocations scale linearly with iterations (no retention/leaks).
/// Uses OperationsPerInvoke to get accurate per-request metrics.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10, invocationCount: 1)]
[ExcludeFromCodeCoverage]
[JsonExporterAttribute.Brief]
[AsciiDocExporter]
[KeepBenchmarkFiles]
public class MemoryLeakBenchmarks
{
    private IMediator _microMediator = null!;
    private MediatR.IMediator _mediatr = null!;
    private IServiceProvider _microProvider = null!;
    private IServiceProvider _mediatrProvider = null!;

    [Params(1_000, 10_000, 100_000)]
    public int Iterations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Setup MicroMediator - no logging to match basic benchmarks
        var microServices = new ServiceCollection();
        microServices.AddMediator()
            .AddSingletonHandler<LeakTestQuery, int, LeakTestQueryHandler>()
            .AddSingletonHandler<LeakTestCommand, bool, LeakTestCommandHandler>();

        _microProvider = microServices.BuildServiceProvider();
        _microMediator = _microProvider.GetRequiredService<IMediator>();

        // Setup MediatR
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MemoryLeakBenchmarks>());

        _mediatrProvider = mediatrServices.BuildServiceProvider();
        _mediatr = _mediatrProvider.GetRequiredService<MediatR.IMediator>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        (_microProvider as IDisposable)?.Dispose();
        (_mediatrProvider as IDisposable)?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task MicroMediator_LongRunning()
    {
        for (int i = 0; i < Iterations; i++)
        {
            await _microMediator.SendAsync(new LeakTestQuery { Value = i });
        }
        // BenchmarkDotNet's MemoryDiagnoser tracks allocations naturally
        // No memory leaks = allocations stay proportional to iterations
    }

    [Benchmark]
    public async Task MediatR_LongRunning()
    {
        for (int i = 0; i < Iterations; i++)
        {
            await _mediatr.Send(new MediatrLeakTestQuery { Value = i });
        }
    }

    [Benchmark]
    public async Task MicroMediator_WithVariedRequests()
    {
        for (int i = 0; i < Iterations; i++)
        {
            // Alternate between different request types to test cache behavior
            if (i % 2 == 0)
            {
                await _microMediator.SendAsync(new LeakTestQuery { Value = i });
            }
            else
            {
                await _microMediator.SendAsync(new LeakTestCommand { Id = i });
            }
        }
    }

    // MicroMediator types
    public record LeakTestQuery : Abstractions.IRequest<int>
    {
        public int Value { get; init; }
    }

    public record LeakTestCommand : Abstractions.IRequest<bool>
    {
        public int Id { get; init; }
    }

    public class LeakTestQueryHandler : Abstractions.IRequestHandler<LeakTestQuery, int>
    {
        public ValueTask<int> HandleAsync(LeakTestQuery request, CancellationToken cancellationToken) => ValueTask.FromResult(request.Value * 2);
    }

    public class LeakTestCommandHandler : Abstractions.IRequestHandler<LeakTestCommand, bool>
    {
        public ValueTask<bool> HandleAsync(LeakTestCommand request, CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }

    // MediatR types
    public record MediatrLeakTestQuery : MediatR.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class MediatrLeakTestQueryHandler : MediatR.IRequestHandler<MediatrLeakTestQuery, int>
    {
        public Task<int> Handle(MediatrLeakTestQuery request, CancellationToken cancellationToken) => Task.FromResult(request.Value * 2);
    }
}
