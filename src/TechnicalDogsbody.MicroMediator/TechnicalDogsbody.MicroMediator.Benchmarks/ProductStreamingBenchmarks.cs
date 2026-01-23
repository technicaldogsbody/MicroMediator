
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
/// Realistic benchmark comparing streaming vs loading products.
/// Shows memory efficiency for large object graphs.
/// </summary>
[MemoryDiagnoser]
[ExcludeFromCodeCoverage]
[JsonExporterAttribute.Brief]
[AsciiDocExporter]
[KeepBenchmarkFiles]
public class ProductStreamingBenchmarks
{
    private IMediator _mediator = null!;

    [Params(100, 1000, 5000)]
    public int ProductCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddMediator()
            .AddStreamHandler<StreamProductsQuery, Product, StreamProductsHandler>()
            .AddHandler<LoadProductsQuery, List<Product>, LoadProductsHandler>();

        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    [Benchmark(Baseline = true)]
    public async Task<List<Product>> LoadAll_Products()
    {
        var query = new LoadProductsQuery { Count = ProductCount };
        return await _mediator.SendAsync(query);
    }

    [Benchmark]
    public async Task<List<Product>> Stream_Products_ToList()
    {
        var query = new StreamProductsQuery { Count = ProductCount };
        var results = new List<Product>(ProductCount);

        await foreach (var product in _mediator.StreamAsync(query))
        {
            results.Add(product);
        }

        return results;
    }

    [Benchmark]
    public async Task<decimal> Stream_Products_CalculateTotal()
    {
        var query = new StreamProductsQuery { Count = ProductCount };
        decimal total = 0m;

        await foreach (var product in _mediator.StreamAsync(query))
        {
            total += product.Price * product.StockQuantity;
        }

        return total;
    }

    [Benchmark]
    public async Task<int> Stream_Products_TakeFirst50()
    {
        var query = new StreamProductsQuery { Count = ProductCount };
        int count = 0;

        await foreach (var product in _mediator.StreamAsync(query))
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

// Product model for benchmarking
public record Product
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
    public required string Category { get; init; }
}

// Streaming query
public record StreamProductsQuery : IStreamRequest<Product>
{
    public int Count { get; init; }
}

// Traditional query
public record LoadProductsQuery : IRequest<List<Product>>
{
    public int Count { get; init; }
}

// Streaming handler
[ExcludeFromCodeCoverage]
public class StreamProductsHandler : IStreamRequestHandler<StreamProductsQuery, Product>
{
    public async IAsyncEnumerable<Product> HandleAsync(
        StreamProductsQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string[] categories = ["Electronics", "Clothing", "Food"];

        for (int i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();

            yield return new Product
            {
                Id = i,
                Name = $"Product {i}",
                Description = $"Description for product {i} with some additional text to make it more realistic",
                Price = 10m + (i % 100),
                StockQuantity = i % 50,
                Category = categories[i % categories.Length]
            };
        }
    }
}

// Traditional handler
[ExcludeFromCodeCoverage]
public class LoadProductsHandler : IRequestHandler<LoadProductsQuery, List<Product>>
{
    public async ValueTask<List<Product>> HandleAsync(
        LoadProductsQuery request,
        CancellationToken cancellationToken)
    {
        string[] categories = ["Electronics", "Clothing", "Food"];
        var results = new List<Product>(request.Count);

        for (int i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();

            results.Add(new Product
            {
                Id = i,
                Name = $"Product {i}",
                Description = $"Description for product {i} with some additional text to make it more realistic",
                Price = 10m + (i % 100),
                StockQuantity = i % 50,
                Category = categories[i % categories.Length]
            });
        }

        return results;
    }
}
