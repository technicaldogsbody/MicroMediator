namespace TechnicalDogsbody.MicroMediator.Examples.StreamingQueries;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using TechnicalDogsbody.MicroMediator.Abstractions;
using TechnicalDogsbody.MicroMediator.Examples.Models;

/// <summary>
/// Streaming query to get paginated customer orders.
/// Useful for report generation or data exports.
/// </summary>
[ExcludeFromCodeCoverage]
public record StreamCustomerOrdersQuery : IStreamRequest<Order>
{
    public required string CustomerEmail { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}

/// <summary>
/// Handler that streams customer orders efficiently.
/// </summary>
[ExcludeFromCodeCoverage]
public class StreamCustomerOrdersHandler : IStreamRequestHandler<StreamCustomerOrdersQuery, Order>
{
    // Simulated order database
    private static readonly List<Order> _orders = GenerateOrders(1000);

    public async IAsyncEnumerable<Order> HandleAsync(
        StreamCustomerOrdersQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var order in _orders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Filter by customer email
            if (!order.CustomerEmail.Equals(request.CustomerEmail, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Filter by date range
            if (request.FromDate.HasValue && order.CreatedAt < request.FromDate.Value)
            {
                continue;
            }

            if (request.ToDate.HasValue && order.CreatedAt > request.ToDate.Value)
            {
                continue;
            }

            // Simulate database latency
            await Task.Delay(1, cancellationToken);

            yield return order;
        }
    }

    private static List<Order> GenerateOrders(int count)
    {
        string[] emails = ["john@example.com", "jane@example.com", "bob@example.com"];
        var orders = new List<Order>(count);

        for (int i = 0; i < count; i++)
        {
            int itemCount = (i % 5) + 1;
            var items = new List<OrderItem>();
            decimal total = 0;

            for (int j = 0; j < itemCount; j++)
            {
                var item = new OrderItem
                {
                    ProductId = (i * 10) + j,
                    ProductName = $"Product {(i * 10) + j}",
                    Quantity = (j % 3) + 1,
                    UnitPrice = 10m + j
                };
                items.Add(item);
                total += item.Subtotal;
            }

            orders.Add(new Order
            {
                Id = i + 1,
                CustomerEmail = emails[i % emails.Length],
                Items = items,
                TotalAmount = total,
                Status = (OrderStatus)(i % 5),
                CreatedAt = DateTime.UtcNow.AddDays(-i),
                CompletedAt = i % 2 == 0 ? DateTime.UtcNow.AddDays(-i + 1) : null
            });
        }

        return orders;
    }
}
