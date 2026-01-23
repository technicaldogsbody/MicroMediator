namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

/// <summary>
/// Tests exception handling resilience and performance impact.
/// Handlers intentionally throw exceptions to test recovery.
/// Each benchmark method executes 1,000 requests to test sustained error handling.
/// </summary>
[MemoryDiagnoser]
[ExcludeFromCodeCoverage]
[JsonExporterAttribute.Brief]
[AsciiDocExporter]
[KeepBenchmarkFiles]
public class ExceptionHandlingBenchmarks
{
    private const int RequestsPerBenchmark = 1000;
    
    private IMediator _microMediator = null!;
    private MediatR.IMediator _mediatr = null!;

    [Params(0.0, 0.1, 0.5)]
    public double FailureRate { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Setup MicroMediator WITHOUT logging to match MediatR
        var microServices = new ServiceCollection();
        microServices.AddMediator()
            .AddHandler<FlakyQuery, int, FlakyQueryHandler>();
        
        // Ensure logging is available but not used
        microServices.AddLogging();

        var microProvider = microServices.BuildServiceProvider();
        _microMediator = microProvider.GetRequiredService<IMediator>();

        // Setup MediatR
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => 
        {
            cfg.RegisterServicesFromAssemblyContaining<ExceptionHandlingBenchmarks>();
            cfg.Lifetime = ServiceLifetime.Transient; // Match MicroMediator's transient handlers
        });
        
        mediatrServices.AddLogging();

        var mediatrProvider = mediatrServices.BuildServiceProvider();
        _mediatr = mediatrProvider.GetRequiredService<MediatR.IMediator>();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = RequestsPerBenchmark)]
    public async Task<int> MicroMediator_WithErrors()
    {
        int successCount = 0;
        int failedCount = 0;

        for (int i = 0; i < RequestsPerBenchmark; i++)
        {
            try
            {
                int result = await _microMediator.SendAsync(new FlakyQuery { FailureRate = FailureRate, Value = i });
                successCount++;
            }
            catch (InvalidOperationException)
            {
                failedCount++;
            }
        }

        return successCount + failedCount; // Return total to prevent dead code elimination
    }

    [Benchmark(OperationsPerInvoke = RequestsPerBenchmark)]
    public async Task<int> MediatR_WithErrors()
    {
        int successCount = 0;
        int failedCount = 0;

        for (int i = 0; i < 1000; i++)
        {
            try
            {
                int result = await _mediatr.Send(new MediatrFlakyQuery { FailureRate = FailureRate, Value = i });
                successCount++;
            }
            catch (InvalidOperationException)
            {
                failedCount++;
            }
        }

        return successCount + failedCount; // Return total to prevent dead code elimination
    }

    [Benchmark(OperationsPerInvoke = RequestsPerBenchmark)]
    public async Task<int> MicroMediator_ParallelWithErrors()
    {
        int successCount = 0;

        var tasks = new Task[RequestsPerBenchmark];
        for (int i = 0; i < RequestsPerBenchmark; i++)
        {
            int value = i;
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    await _microMediator.SendAsync(new FlakyQuery { FailureRate = FailureRate, Value = value });
                    Interlocked.Increment(ref successCount);
                }
                catch (InvalidOperationException)
                {
                    // Expected
                }
            });
        }

        await Task.WhenAll(tasks);
        return successCount;
    }

    // MicroMediator types
    public record FlakyQuery : Abstractions.IRequest<int>
    {
        public double FailureRate { get; init; }
        public int Value { get; init; }
    }

    public class FlakyQueryHandler : Abstractions.IRequestHandler<FlakyQuery, int>
    {
        private static int _counter;
        private readonly Random _random = new(Interlocked.Increment(ref _counter));

        public ValueTask<int> HandleAsync(FlakyQuery request, CancellationToken cancellationToken)
        {
            if (_random.NextDouble() < request.FailureRate)
            {
                throw new InvalidOperationException($"Simulated failure for value {request.Value}");
            }

            return ValueTask.FromResult(request.Value * 2);
        }
    }

    // MediatR types
    public record MediatrFlakyQuery : MediatR.IRequest<int>
    {
        public double FailureRate { get; init; }
        public int Value { get; init; }
    }

    public class MediatrFlakyQueryHandler : MediatR.IRequestHandler<MediatrFlakyQuery, int>
    {
        private static int _counter;
        private readonly Random _random = new(Interlocked.Increment(ref _counter));

        public Task<int> Handle(MediatrFlakyQuery request, CancellationToken cancellationToken)
        {
            if (_random.NextDouble() < request.FailureRate)
            {
                throw new InvalidOperationException($"Simulated failure for value {request.Value}");
            }

            return Task.FromResult(request.Value * 2);
        }
    }
}
