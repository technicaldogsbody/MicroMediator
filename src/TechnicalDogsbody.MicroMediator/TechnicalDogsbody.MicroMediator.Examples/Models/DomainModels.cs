using System.Diagnostics.CodeAnalysis;

namespace TechnicalDogsbody.MicroMediator.Examples.Models;

/// <summary>
/// Product entity
/// </summary>
[ExcludeFromCodeCoverage]
public record Product
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
    public string? Category { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Order entity
/// </summary>
[ExcludeFromCodeCoverage]
public record Order
{
    public int Id { get; init; }
    public required string CustomerEmail { get; init; }
    public List<OrderItem> Items { get; init; } = [];
    public decimal TotalAmount { get; init; }
    public OrderStatus Status { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
}

/// <summary>
/// Order item
/// </summary>
[ExcludeFromCodeCoverage]
public record OrderItem
{
    public int ProductId { get; init; }
    public required string ProductName { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Subtotal => Quantity * UnitPrice;
}

/// <summary>
/// Order status enum
/// </summary>
public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}

/// <summary>
/// Customer entity
/// </summary>
[ExcludeFromCodeCoverage]
public record Customer
{
    public int Id { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public string? PhoneNumber { get; init; }
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
}
