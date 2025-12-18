using System.Diagnostics.CodeAnalysis;
using TechnicalDogsbody.MicroMediator.Abstractions;
using TechnicalDogsbody.MicroMediator.Examples.Models;

namespace TechnicalDogsbody.MicroMediator.Examples.Queries;

/// <summary>
/// Query to search products with optional filters
/// </summary>
[ExcludeFromCodeCoverage]
public record SearchProductsQuery : IRequest<List<Product>>
{
    public string? SearchTerm { get; init; }
    public string? Category { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool OnlyInStock { get; init; } = true;
}

/// <summary>
/// Handler for SearchProductsQuery
/// </summary>
[ExcludeFromCodeCoverage]
public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, List<Product>>
{
    private readonly ILogger<SearchProductsQueryHandler> _logger;

    // Simulated database
    private static readonly List<Product> Products =
    [
        new() { Id = 1, Name = "Laptop", Description = "High-performance laptop", Price = 1299.99m, StockQuantity = 15, Category = "Electronics" },
        new() { Id = 2, Name = "Wireless Mouse", Description = "Ergonomic wireless mouse", Price = 29.99m, StockQuantity = 50, Category = "Electronics" },
        new() { Id = 3, Name = "Mechanical Keyboard", Description = "RGB mechanical keyboard", Price = 149.99m, StockQuantity = 25, Category = "Electronics" },
        new() { Id = 4, Name = "USB-C Cable", Description = "6ft USB-C charging cable", Price = 19.99m, StockQuantity = 100, Category = "Accessories" },
        new() { Id = 5, Name = "Monitor", Description = "27-inch 4K monitor", Price = 399.99m, StockQuantity = 10, Category = "Electronics" },
        new() { Id = 6, Name = "Desk Lamp", Description = "LED desk lamp", Price = 45.99m, StockQuantity = 0, Category = "Office" }
    ];

    public SearchProductsQueryHandler(ILogger<SearchProductsQueryHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<List<Product>> HandleAsync(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Searching products with filters: SearchTerm={SearchTerm}, Category={Category}", 
            request.SearchTerm, request.Category);

        // Simulate database delay
        await Task.Delay(100, cancellationToken);

        var query = Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(p => 
                p.Name.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(p => p.Category == request.Category);
        }

        if (request.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= request.MinPrice.Value);
        }

        if (request.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= request.MaxPrice.Value);
        }

        if (request.OnlyInStock)
        {
            query = query.Where(p => p.StockQuantity > 0);
        }

        return query.ToList();
    }
}
