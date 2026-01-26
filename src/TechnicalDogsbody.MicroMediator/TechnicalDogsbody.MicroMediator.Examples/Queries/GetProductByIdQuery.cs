
namespace TechnicalDogsbody.MicroMediator.Examples.Queries;

using System.Diagnostics.CodeAnalysis;
using TechnicalDogsbody.MicroMediator.Abstractions;
using TechnicalDogsbody.MicroMediator.Examples.Models;

/// <summary>
/// Query to get a product by ID with caching enabled
/// </summary>
[ExcludeFromCodeCoverage]
public record GetProductByIdQuery(int ProductId) : IRequest<Product?>, ICacheableRequest
{
    public string CacheKey => $"Product_{ProductId}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}

/// <summary>
/// Handler for GetProductByIdQuery
/// </summary>
[ExcludeFromCodeCoverage]
public class GetProductByIdQueryHandler(ILogger<GetProductByIdQueryHandler> logger)
    : IRequestHandler<GetProductByIdQuery, Product?>
{
    // Simulated database
    private static readonly List<Product> _products =
    [
        new() { Id = 1, Name = "Laptop", Description = "High-performance laptop", Price = 1299.99m, StockQuantity = 15, Category = "Electronics" },
    new() { Id = 2, Name = "Wireless Mouse", Description = "Ergonomic wireless mouse", Price = 29.99m, StockQuantity = 50, Category = "Electronics" },
    new() { Id = 3, Name = "Mechanical Keyboard", Description = "RGB mechanical keyboard", Price = 149.99m, StockQuantity = 25, Category = "Electronics" },
    new() { Id = 4, Name = "USB-C Cable", Description = "6ft USB-C charging cable", Price = 19.99m, StockQuantity = 100, Category = "Accessories" },
    new() { Id = 5, Name = "Monitor", Description = "27-inch 4K monitor", Price = 399.99m, StockQuantity = 10, Category = "Electronics" }
    ];

    public async ValueTask<Product?> HandleAsync(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching product {ProductId} from database", request.ProductId);

        // Simulate database delay
        await Task.Delay(50, cancellationToken);

        return _products.FirstOrDefault(p => p.Id == request.ProductId);
    }
}
