namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using System;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

/// <summary>
/// Stress tests for streaming with massive datasets.
/// Tests memory stability with 1M+ item streams.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
[ExcludeFromCodeCoverage]
[JsonExporterAttribute.Brief]
[AsciiDocExporter]
[KeepBenchmarkFiles]
public class StreamingStressBenchmarks
{
    private IMediator _mediator = null!;

    [Params(100_000, 1_000_000)]
    public int StreamSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddSingletonStreamHandler<MassiveStreamQuery, int, MassiveStreamHandler>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Benchmark(Baseline = true)]
    public async Task<long> ProcessHugeStream_WithEarlyExit()
    {
        long sum = 0;
        int count = 0;

        await foreach (int item in _mediator.StreamAsync(new MassiveStreamQuery { Count = StreamSize }))
        {
            sum += item;
            if (++count >= 1000)
            {
                break;
            }
        }

        return sum;
    }

    [Benchmark]
    public async Task<long> ProcessHugeStream_Complete()
    {
        long sum = 0;

        await foreach (int item in _mediator.StreamAsync(new MassiveStreamQuery { Count = StreamSize }))
        {
            sum += item;
        }

        return sum;
    }

    [Benchmark]
    public async Task<int> ProcessHugeStream_CountOnly()
    {
        int count = 0;

        await foreach (int _ in _mediator.StreamAsync(new MassiveStreamQuery { Count = StreamSize }))
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public async Task<List<int>> ProcessHugeStream_ToList_Limited()
    {
        var results = new List<int>(10_000);
        int count = 0;

        await foreach (int item in _mediator.StreamAsync(new MassiveStreamQuery { Count = StreamSize }))
        {
            results.Add(item);
            if (++count >= 10_000)
            {
                break;
            }
        }

        return results;
    }

    [Benchmark]
    public async Task<long> ProcessHugeStream_WithCancellation()
    {
        using var cts = new CancellationTokenSource();
        long sum = 0;
        int count = 0;

        try
        {
            await foreach (int item in _mediator.StreamAsync(new MassiveStreamQuery { Count = StreamSize }, cts.Token))
            {
                sum += item;
                if (++count >= 50_000)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        return sum;
    }
}

// Streaming request
public record MassiveStreamQuery : Abstractions.IStreamRequest<int>
{
    public int Count { get; init; }
}

// Streaming handler
[ExcludeFromCodeCoverage]
public class MassiveStreamHandler : Abstractions.IStreamRequestHandler<MassiveStreamQuery, int>
{
    public async IAsyncEnumerable<int> HandleAsync(
        MassiveStreamQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Yield occasionally to simulate real async work
            if (i % 1000 == 0)
            {
                await Task.Yield();
            }

            yield return i;
        }
    }
}
