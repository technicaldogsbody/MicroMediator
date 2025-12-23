namespace TechnicalDogsbody.MicroMediator.Examples.StreamingQueries;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using TechnicalDogsbody.MicroMediator.Abstractions;
using TechnicalDogsbody.MicroMediator.Examples.Models;

/// <summary>
/// Streaming query to export all products in a category.
/// Returns products one at a time to avoid loading thousands into memory.
/// </summary>
[ExcludeFromCodeCoverage]
public record ExportProductsByCategoryQuery : IStreamRequest<Product>
{
    public string? Category { get; init; }
    public bool ActiveOnly { get; init; } = true;
}

/// <summary>
/// Handler that streams products from a simulated repository.
/// In real scenarios, this would stream from database using IAsyncEnumerable.
/// </summary>
[ExcludeFromCodeCoverage]
public class ExportProductsByCategoryHandler : IStreamRequestHandler<ExportProductsByCategoryQuery, Product>
{
    // Simulated product database
    private static readonly List<Product> _products = GenerateProducts(2500);

    public async IAsyncEnumerable<Product> HandleAsync(
        ExportProductsByCategoryQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Simulate database streaming
        foreach (var product in _products)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Filter by category and active status
            if (request.Category is not null && product.Category != request.Category)
            {
                continue;
            }

            if (request.ActiveOnly && !product.IsActive)
            {
                continue;
            }

            // Simulate database latency (remove in real implementation)
            await Task.Delay(1, cancellationToken);

            yield return product;
        }
    }

    private static List<Product> GenerateProducts(int count)
    {
        string[] categories = ["Electronics", "Clothing", "Food", "Books", "Toys"];
        var products = new List<Product>(count);

        for (int i = 0; i < count; i++)
        {
            products.Add(new Product
            {
                Id = i + 1,
                Name = $"Product {i + 1}",
                Description = $"Description for product {i + 1}",
                Price = 10m + (i % 100),
                StockQuantity = i % 50,
                Category = categories[i % categories.Length],
                IsActive = i % 10 != 0, // 10% inactive
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        return products;
    }
}
