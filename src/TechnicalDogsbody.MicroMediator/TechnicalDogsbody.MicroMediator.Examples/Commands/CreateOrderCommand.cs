using FluentValidation;
using System.Diagnostics.CodeAnalysis;
using TechnicalDogsbody.MicroMediator.Abstractions;

namespace TechnicalDogsbody.MicroMediator.Examples.Commands;

/// <summary>
/// Command to create a new order
/// </summary>
[ExcludeFromCodeCoverage]
public record CreateOrderCommand : IRequest<OrderResult>
{
    public required string CustomerEmail { get; init; }
    public List<OrderItemDto> Items { get; init; } = [];
}

/// <summary>
/// DTO for order item in command
/// </summary>
[ExcludeFromCodeCoverage]
public record OrderItemDto
{
    public int ProductId { get; init; }
    public int Quantity { get; init; }
}

/// <summary>
/// Result of order creation
/// </summary>
[ExcludeFromCodeCoverage]
public record OrderResult
{
    public bool Success { get; init; }
    public int? OrderId { get; init; }
    public string? ErrorMessage { get; init; }
    public decimal? TotalAmount { get; init; }

    public static OrderResult Succeeded(int orderId, decimal totalAmount) => new()
    {
        Success = true,
        OrderId = orderId,
        TotalAmount = totalAmount
    };

    public static OrderResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Validator for CreateOrderCommand
/// </summary>
[ExcludeFromCodeCoverage]
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("Customer email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Order must contain at least one item")
            .Must(items => items.Count <= 50).WithMessage("Order cannot contain more than 50 items");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ProductId)
                    .GreaterThan(0).WithMessage("Invalid product ID");

                item.RuleFor(x => x.Quantity)
                    .InclusiveBetween(1, 100).WithMessage("Quantity must be between 1 and 100");
            });
    }
}

/// <summary>
/// Handler for CreateOrderCommand
/// </summary>
[ExcludeFromCodeCoverage]
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderResult>
{
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    // Simulated product database
    private static readonly Dictionary<int, (string Name, decimal Price, int Stock)> _products = new()
    {
        { 1, ("Laptop", 1299.99m, 15) },
        { 2, ("Wireless Mouse", 29.99m, 50) },
        { 3, ("Mechanical Keyboard", 149.99m, 25) },
        { 4, ("USB-C Cable", 19.99m, 100) },
        { 5, ("Monitor", 399.99m, 10) }
    };

    private static int _nextOrderId = 100;

    public CreateOrderCommandHandler(ILogger<CreateOrderCommandHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<OrderResult> HandleAsync(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating order for customer {CustomerEmail} with {ItemCount} items",
            request.CustomerEmail, request.Items.Count);

        // Validate product availability
        foreach (var item in request.Items)
        {
            if (!_products.TryGetValue(item.ProductId, out var product))
            {
                return OrderResult.Failed($"Product {item.ProductId} not found");
            }

            if (product.Stock < item.Quantity)
            {
                return OrderResult.Failed($"Insufficient stock for product {product.Name}");
            }
        }

        // Calculate total
        decimal totalAmount = request.Items.Sum(item =>
        {
            (_, decimal price, _) = _products[item.ProductId];
            return price * item.Quantity;
        });

        // Simulate database operation
        await Task.Delay(200, cancellationToken);

        int orderId = Interlocked.Increment(ref _nextOrderId);

        _logger.LogInformation("Order {OrderId} created successfully. Total: {TotalAmount:C}", orderId, totalAmount);

        return OrderResult.Succeeded(orderId, totalAmount);
    }
}
