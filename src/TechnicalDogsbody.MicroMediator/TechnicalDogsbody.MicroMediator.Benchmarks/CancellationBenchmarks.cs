namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

/// <summary>
/// Tests cancellation token behaviour and cleanup.
/// Verifies graceful cancellation without resource leaks.
/// </summary>
[MemoryDiagnoser]
[ExcludeFromCodeCoverage]
[JsonExporterAttribute.Brief]
[AsciiDocExporter]
[KeepBenchmarkFiles]
public class CancellationBenchmarks
{
    private IMediator _microMediator = null!;
    private MediatR.IMediator _mediatr = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup MicroMediator
        var microServices = new ServiceCollection();
        microServices.AddMediator()
            .AddHandler<LongRunningQuery, int, LongRunningQueryHandler>()
            .AddHandler<QuickQuery, int, QuickQueryHandler>();

        var microProvider = microServices.BuildServiceProvider();
        _microMediator = microProvider.GetRequiredService<IMediator>();

        // Setup MediatR
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CancellationBenchmarks>());

        var mediatrProvider = mediatrServices.BuildServiceProvider();
        _mediatr = mediatrProvider.GetRequiredService<MediatR.IMediator>();
    }

    [Benchmark(Baseline = true)]
    public async Task MicroMediator_CancelAfter100ms()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        try
        {
            await _microMediator.SendAsync(new LongRunningQuery { DurationMs = 1000 }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    [Benchmark]
    public async Task MediatR_CancelAfter100ms()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        try
        {
            await _mediatr.Send(new MediatrLongRunningQuery { DurationMs = 1000 }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    [Benchmark]
    public async Task MicroMediator_CancelManyRequests()
    {
        using var cts = new CancellationTokenSource();
        var tasks = new Task[1000];

        for (int i = 0; i < 1000; i++)
        {
            int index = i;
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    await _microMediator.SendAsync(new LongRunningQuery { DurationMs = 5000 }, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            });

            // Cancel after starting 500 tasks
            if (i == 500)
            {
                cts.Cancel();
            }
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task MicroMediator_RespectsCancellationInLoop()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        int completedCount = 0;
        try
        {
            for (int i = 0; i < 1000; i++)
            {
                await _microMediator.SendAsync(new QuickQuery { Value = i }, cts.Token);
                completedCount++;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    // MicroMediator types
    public record LongRunningQuery : Abstractions.IRequest<int>
    {
        public int DurationMs { get; init; }
    }

    public record QuickQuery : Abstractions.IRequest<int>
    {
        public int Value { get; init; }
    }

    public class LongRunningQueryHandler : Abstractions.IRequestHandler<LongRunningQuery, int>
    {
        public async ValueTask<int> HandleAsync(LongRunningQuery request, CancellationToken cancellationToken)
        {
            await Task.Delay(request.DurationMs, cancellationToken);
            return 42;
        }
    }

    public class QuickQueryHandler : Abstractions.IRequestHandler<QuickQuery, int>
    {
        public ValueTask<int> HandleAsync(QuickQuery request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(request.Value * 2);
        }
    }

    // MediatR types
    public record MediatrLongRunningQuery : MediatR.IRequest<int>
    {
        public int DurationMs { get; init; }
    }

    public class MediatrLongRunningQueryHandler : MediatR.IRequestHandler<MediatrLongRunningQuery, int>
    {
        public async Task<int> Handle(MediatrLongRunningQuery request, CancellationToken cancellationToken)
        {
            await Task.Delay(request.DurationMs, cancellationToken);
            return 42;
        }
    }
}
