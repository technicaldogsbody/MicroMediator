using FluentValidation;
using System.Diagnostics.CodeAnalysis;
using TechnicalDogsbody.MicroMediator.Abstractions;

namespace TechnicalDogsbody.MicroMediator.Examples.Commands;

/// <summary>
/// Command to update product price
/// </summary>
[ExcludeFromCodeCoverage]
public record UpdateProductPriceCommand : IRequest<UpdateResult>
{
    public int ProductId { get; init; }
    public decimal NewPrice { get; init; }
    public string? Reason { get; init; }
}

/// <summary>
/// Generic update result
/// </summary>
[ExcludeFromCodeCoverage]
public record UpdateResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static UpdateResult Succeeded(string? message = null) => new()
    {
        Success = true,
        Message = message ?? "Update successful"
    };

    public static UpdateResult Failed(string message) => new()
    {
        Success = false,
        Message = message
    };
}

/// <summary>
/// Validator for UpdateProductPriceCommand
/// </summary>
[ExcludeFromCodeCoverage]
public class UpdateProductPriceCommandValidator : AbstractValidator<UpdateProductPriceCommand>
{
    public UpdateProductPriceCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0).WithMessage("Product ID must be positive");

        RuleFor(x => x.NewPrice)
            .GreaterThan(0).WithMessage("Price must be greater than zero")
            .LessThanOrEqualTo(100000).WithMessage("Price cannot exceed $100,000");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters");
    }
}

/// <summary>
/// Handler for UpdateProductPriceCommand
/// </summary>
[ExcludeFromCodeCoverage]
public class UpdateProductPriceCommandHandler : IRequestHandler<UpdateProductPriceCommand, UpdateResult>
{
    private readonly ILogger<UpdateProductPriceCommandHandler> _logger;

    // Simulated database
    private static readonly Dictionary<int, decimal> ProductPrices = new()
    {
        { 1, 1299.99m },
        { 2, 29.99m },
        { 3, 149.99m },
        { 4, 19.99m },
        { 5, 399.99m }
    };

    public UpdateProductPriceCommandHandler(ILogger<UpdateProductPriceCommandHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<UpdateResult> HandleAsync(UpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating price for product {ProductId} to {NewPrice:C}. Reason: {Reason}",
            request.ProductId, request.NewPrice, request.Reason ?? "Not specified");

        if (!ProductPrices.ContainsKey(request.ProductId))
        {
            return UpdateResult.Failed($"Product {request.ProductId} not found");
        }

        var oldPrice = ProductPrices[request.ProductId];

        // Simulate database operation
        await Task.Delay(100, cancellationToken);

        ProductPrices[request.ProductId] = request.NewPrice;

        _logger.LogInformation("Product {ProductId} price updated from {OldPrice:C} to {NewPrice:C}",
            request.ProductId, oldPrice, request.NewPrice);

        return UpdateResult.Succeeded($"Price updated from {oldPrice:C} to {request.NewPrice:C}");
    }
}
