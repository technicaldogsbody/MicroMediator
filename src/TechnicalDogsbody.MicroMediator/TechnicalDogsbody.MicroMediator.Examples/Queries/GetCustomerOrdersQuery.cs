
namespace TechnicalDogsbody.MicroMediator.Examples.Queries;

using System.Diagnostics.CodeAnalysis;
using TechnicalDogsbody.MicroMediator.Abstractions;
using TechnicalDogsbody.MicroMediator.Examples.Models;

/// <summary>
/// Query to get customer order history with caching
/// </summary>
[ExcludeFromCodeCoverage]
public record GetCustomerOrdersQuery(string CustomerEmail) : IRequest<List<Order>>, ICacheableRequest
{
    public string CacheKey => $"CustomerOrders_{CustomerEmail}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

/// <summary>
/// Handler for GetCustomerOrdersQuery
/// </summary>
[ExcludeFromCodeCoverage]
[method: ExcludeFromCodeCoverage]
public class GetCustomerOrdersQueryHandler(ILogger<GetCustomerOrdersQueryHandler> logger)
    : IRequestHandler<GetCustomerOrdersQuery, List<Order>>
{
    // Simulated database
    private static readonly List<Order> _orders =
    [
        new()
    {
        Id = 1,
        CustomerEmail = "john.doe@example.com",
        Items =
        [
            new() { ProductId = 1, ProductName = "Laptop", Quantity = 1, UnitPrice = 1299.99m },
            new() { ProductId = 2, ProductName = "Wireless Mouse", Quantity = 2, UnitPrice = 29.99m }
        ],
        TotalAmount = 1359.97m,
        Status = OrderStatus.Delivered,
        CreatedAt = DateTime.UtcNow.AddDays(-10),
        CompletedAt = DateTime.UtcNow.AddDays(-7)
    },
    new()
    {
        Id = 2,
        CustomerEmail = "john.doe@example.com",
        Items =
        [
            new() { ProductId = 3, ProductName = "Mechanical Keyboard", Quantity = 1, UnitPrice = 149.99m }
        ],
        TotalAmount = 149.99m,
        Status = OrderStatus.Shipped,
        CreatedAt = DateTime.UtcNow.AddDays(-2)
    }
    ];

    [ExcludeFromCodeCoverage]
    public async ValueTask<List<Order>> HandleAsync(GetCustomerOrdersQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching orders for customer {CustomerEmail}", request.CustomerEmail);

        // Simulate database delay
        await Task.Delay(150, cancellationToken);

        return _orders
            .Where(o => o.CustomerEmail.Equals(request.CustomerEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(o => o.CreatedAt)
            .ToList();
    }
}
