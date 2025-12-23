
namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Benchmarks comparing streaming vs loading all results into memory.
/// Demonstrates memory efficiency benefits of IAsyncEnumerable.
/// </summary>
[MemoryDiagnoser]
[ExcludeFromCodeCoverage]
public class StreamingBenchmarks
{
    private IMediator _mediator = null!;

    [Params(100, 1000, 10000)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddStreamHandler<StreamNumbersQuery, int, StreamNumbersHandler>()
            .AddHandler<LoadNumbersQuery, List<int>, LoadNumbersHandler>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Benchmark(Baseline = true)]
    public async Task<List<int>> LoadAll_ToList()
    {
        var query = new LoadNumbersQuery { Count = ItemCount };
        return await _mediator.SendAsync(query);
    }

    [Benchmark]
    public async Task<List<int>> Stream_ToList()
    {
        var query = new StreamNumbersQuery { Count = ItemCount };
        var results = new List<int>(ItemCount);

        await foreach (int item in _mediator.StreamAsync(query))
        {
            results.Add(item);
        }

        return results;
    }

    [Benchmark]
    public async Task<int> Stream_ProcessItems()
    {
        var query = new StreamNumbersQuery { Count = ItemCount };
        int sum = 0;

        await foreach (int item in _mediator.StreamAsync(query))
        {
            sum += item;
        }

        return sum;
    }

    [Benchmark]
    public async Task<int> Stream_TakeFirst100()
    {
        var query = new StreamNumbersQuery { Count = ItemCount };
        int count = 0;

        await foreach (int item in _mediator.StreamAsync(query))
        {
            count++;
            if (count >= 100)
            {
                break;
            }
        }

        return count;
    }
}

// Streaming request
public record StreamNumbersQuery : IStreamRequest<int>
{
    public int Count { get; init; }
}

// Traditional request that loads all into memory
public record LoadNumbersQuery : IRequest<List<int>>
{
    public int Count { get; init; }
}

// Streaming handler
[ExcludeFromCodeCoverage]
public class StreamNumbersHandler : IStreamRequestHandler<StreamNumbersQuery, int>
{
    public async IAsyncEnumerable<int> HandleAsync(
        StreamNumbersQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return i;
        }
    }
}

// Traditional handler that loads everything
[ExcludeFromCodeCoverage]
public class LoadNumbersHandler : IRequestHandler<LoadNumbersQuery, List<int>>
{
    public async ValueTask<List<int>> HandleAsync(
        LoadNumbersQuery request,
        CancellationToken cancellationToken)
    {
        var results = new List<int>(request.Count);

        for (int i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            results.Add(i);
        }

        return results;
    }
}
