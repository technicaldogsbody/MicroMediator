using FluentValidation;
using System.Diagnostics.CodeAnalysis;
using TechnicalDogsbody.MicroMediator.Abstractions;

namespace TechnicalDogsbody.MicroMediator.Examples.Commands;

/// <summary>
/// Command to cancel an order
/// </summary>
[ExcludeFromCodeCoverage]
public record CancelOrderCommand : IRequest<UpdateResult>
{
    public int OrderId { get; init; }
    public required string CancellationReason { get; init; }
}

/// <summary>
/// Validator for CancelOrderCommand
/// </summary>
[ExcludeFromCodeCoverage]
public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .GreaterThan(0).WithMessage("Order ID must be positive");

        RuleFor(x => x.CancellationReason)
            .NotEmpty().WithMessage("Cancellation reason is required")
            .MinimumLength(10).WithMessage("Cancellation reason must be at least 10 characters")
            .MaximumLength(1000).WithMessage("Cancellation reason cannot exceed 1000 characters");
    }
}

/// <summary>
/// Handler for CancelOrderCommand
/// </summary>
[ExcludeFromCodeCoverage]
public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, UpdateResult>
{
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    // Simulated order status database
    private static readonly Dictionary<int, string> _orderStatuses = new()
    {
        { 100, "Pending" },
        { 101, "Processing" },
        { 102, "Shipped" },
        { 103, "Delivered" }
    };

    public CancelOrderCommandHandler(ILogger<CancelOrderCommandHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<UpdateResult> HandleAsync(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to cancel order {OrderId}. Reason: {Reason}",
            request.OrderId, request.CancellationReason);

        if (!_orderStatuses.TryGetValue(request.OrderId, out string? currentStatus))
        {
            return UpdateResult.Failed($"Order {request.OrderId} not found");
        }

        // Business rule: Can't cancel shipped or delivered orders
        if (currentStatus is "Shipped" or "Delivered")
        {
            return UpdateResult.Failed($"Cannot cancel order with status '{currentStatus}'");
        }

        // Simulate database operation
        await Task.Delay(150, cancellationToken);

        _orderStatuses[request.OrderId] = "Cancelled";

        _logger.LogInformation("Order {OrderId} cancelled successfully", request.OrderId);

        return UpdateResult.Succeeded($"Order {request.OrderId} has been cancelled");
    }
}
