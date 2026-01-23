
namespace TechnicalDogsbody.MicroMediator.Benchmarks;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using IMediator = TechnicalDogsbody.MicroMediator.Abstractions.IMediator;

/// <summary>
/// Compares MicroMediator streaming vs MediatR IStreamRequest.
/// MediatR supports streaming via IStreamRequest interface since v12.
/// </summary>
[MemoryDiagnoser]
[ExcludeFromCodeCoverage]
[JsonExporterAttribute.Brief]
[AsciiDocExporter]
[KeepBenchmarkFiles]
public class StreamingComparisonBenchmarks
{
    private IMediator _microMediator = null!;
    private MediatR.IMediator _mediatR = null!;

    [Params(100, 1000, 10000)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Setup MicroMediator
        var microServices = new ServiceCollection();
        microServices.AddMediator()
            .AddStreamHandler<MicroStreamQuery, int, MicroStreamHandler>();

        var microProvider = microServices.BuildServiceProvider();
        _microMediator = microProvider.GetRequiredService<IMediator>();

        // Setup MediatR
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRStreamQuery>());

        var mediatrProvider = mediatrServices.BuildServiceProvider();
        _mediatR = mediatrProvider.GetRequiredService<MediatR.IMediator>();
    }

    [Benchmark(Baseline = true)]
    public async Task<List<int>> MicroMediator_Stream()
    {
        var query = new MicroStreamQuery { Count = ItemCount };
        var results = new List<int>(ItemCount);

        await foreach (int item in _microMediator.StreamAsync(query))
        {
            results.Add(item);
        }

        return results;
    }

    [Benchmark]
    public async Task<List<int>> MediatR_Stream()
    {
        var query = new MediatRStreamQuery { Count = ItemCount };
        var results = new List<int>(ItemCount);

        await foreach (int item in _mediatR.CreateStream(query))
        {
            if (item is int intValue)
            {
                results.Add(intValue);
            }
        }

        return results;
    }

    [Benchmark]
    public async Task<int> MicroMediator_Stream_ProcessOnly()
    {
        var query = new MicroStreamQuery { Count = ItemCount };
        int sum = 0;

        await foreach (int item in _microMediator.StreamAsync(query))
        {
            sum += item;
        }

        return sum;
    }

    [Benchmark]
    public async Task<int> MediatR_Stream_ProcessOnly()
    {
        var query = new MediatRStreamQuery { Count = ItemCount };
        int sum = 0;

        await foreach (int item in _mediatR.CreateStream(query))
        {
            if (item is int intValue)
            {
                sum += intValue;
            }
        }

        return sum;
    }

    [Benchmark]
    public async Task<int> MicroMediator_Stream_EarlyExit()
    {
        var query = new MicroStreamQuery { Count = ItemCount };
        int count = 0;

        await foreach (int item in _microMediator.StreamAsync(query))
        {
            count++;
            if (count >= 50)
            {
                break;
            }
        }

        return count;
    }

    [Benchmark]
    public async Task<int> MediatR_Stream_EarlyExit()
    {
        var query = new MediatRStreamQuery { Count = ItemCount };
        int count = 0;

        await foreach (int item in _mediatR.CreateStream(query))
        {
            count++;
            if (count >= 50)
            {
                break;
            }
        }

        return count;
    }
}

// MicroMediator streaming types
public record MicroStreamQuery : TechnicalDogsbody.MicroMediator.Abstractions.IStreamRequest<int>
{
    public int Count { get; init; }
}

[ExcludeFromCodeCoverage]
public class MicroStreamHandler : TechnicalDogsbody.MicroMediator.Abstractions.IStreamRequestHandler<MicroStreamQuery, int>
{
    public async IAsyncEnumerable<int> HandleAsync(
        MicroStreamQuery request,
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

// MediatR streaming types
public record MediatRStreamQuery : MediatR.IStreamRequest<int>
{
    public int Count { get; init; }
}

[ExcludeFromCodeCoverage]
public class MediatRStreamHandler : MediatR.IStreamRequestHandler<MediatRStreamQuery, int>
{
    public async IAsyncEnumerable<int> Handle(
        MediatRStreamQuery request,
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
