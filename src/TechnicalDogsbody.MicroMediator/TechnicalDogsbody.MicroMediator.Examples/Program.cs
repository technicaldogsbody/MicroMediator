using FluentValidation;
using TechnicalDogsbody.MicroMediator;
using TechnicalDogsbody.MicroMediator.Abstractions;
using TechnicalDogsbody.MicroMediator.Examples;
using TechnicalDogsbody.MicroMediator.Examples.Behaviors;
using TechnicalDogsbody.MicroMediator.Examples.Commands;
using TechnicalDogsbody.MicroMediator.Examples.Providers;
using TechnicalDogsbody.MicroMediator.Examples.Queries;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();

// Register SimpleMediator with fully explicit, zero-reflection configuration
// Behavior execution order (reverse of registration):
// 1. RetryBehavior (outermost)
// 2. AuditBehavior
// 3. PerformanceMonitoringBehavior
// 4. CachingBehavior
// 5. LoggingBehavior
// 6. ValidationBehavior (innermost, closest to handler)
builder.Services
    .AddMediator()
    // Query handlers
    .AddHandler<GetProductByIdQueryHandler>()
    .AddHandler<SearchProductsQueryHandler>()
    .AddHandler<GetCustomerOrdersQueryHandler>()
    // Command handlers
    .AddHandler<CreateOrderCommandHandler>()
    .AddHandler<UpdateProductPriceCommandHandler>()
    .AddHandler<CancelOrderCommandHandler>()
    // Validators
    .AddValidator<CreateOrderCommandValidator>()
    .AddValidator<UpdateProductPriceCommandValidator>()
    .AddValidator<CancelOrderCommandValidator>()
    // Built-in pipelines
    .AddDefaultLoggingPipeline()
    // Caching with default memory cache
    .AddDefaultCachingPipeline()
    // OR: Use custom cache provider (commented out examples)
    //AddCachingPipeline<CustomDictionaryCacheProvider>()
    //.AddCachingPipeline<SimulatedDistributedCacheProvider>()
    // Custom pipelines
    .AddBehavior(typeof(PerformanceMonitoringBehavior<,>))
    .AddBehavior(typeof(AuditBehavior<,>))
    .AddBehavior(typeof(RetryBehavior<,>));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ========================================
// QUERY ENDPOINTS (Read Operations)
// ========================================

app.MapGet("/api/products/{id:int}", async (int id, IMediator mediator) =>
{
    var query = new GetProductByIdQuery(id);
    var product = await mediator.SendAsync(query);

    return product is not null
        ? Results.Ok(product)
        : Results.NotFound(new { message = $"Product {id} not found" });
})
.WithName("GetProduct")
.WithDescription("Get product by ID - results are cached for 10 minutes");

app.MapGet("/api/products", async (
    string? searchTerm,
    string? category,
    decimal? minPrice,
    decimal? maxPrice,
    bool onlyInStock,
    IMediator mediator) =>
{
    var query = new SearchProductsQuery
    {
        SearchTerm = searchTerm,
        Category = category,
        MinPrice = minPrice,
        MaxPrice = maxPrice,
        OnlyInStock = onlyInStock
    };

    var products = await mediator.SendAsync(query);
    return Results.Ok(products);
})
.WithName("SearchProducts")
.WithDescription("Search products with optional filters");

app.MapGet("/api/customers/{email}/orders", async (string email, IMediator mediator) =>
{
    var query = new GetCustomerOrdersQuery(email);
    var orders = await mediator.SendAsync(query);

    return Results.Ok(new
    {
        customerEmail = email,
        orderCount = orders.Count,
        orders
    });
})
.WithName("GetCustomerOrders")
.WithDescription("Get customer order history - results are cached for 5 minutes");

// ========================================
// COMMAND ENDPOINTS (Write Operations)
// ========================================

app.MapPost("/api/orders", async (CreateOrderCommand command, IMediator mediator) =>
{
    try
    {
        var result = await mediator.SendAsync(command);

        return result.Success
            ? Results.Created($"/api/orders/{result.OrderId}", result)
            : Results.BadRequest(result);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new
        {
            errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
        });
    }
})
.WithName("CreateOrder")
.WithDescription("Create a new order - validates items and stock availability");

app.MapPut("/api/products/{id:int}/price", async (int id, decimal newPrice, string? reason, IMediator mediator) =>
{
    try
    {
        var command = new UpdateProductPriceCommand
        {
            ProductId = id,
            NewPrice = newPrice,
            Reason = reason
        };

        var result = await mediator.SendAsync(command);

        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(result);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new
        {
            errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
        });
    }
})
.WithName("UpdateProductPrice")
.WithDescription("Update product price with optional reason");

app.MapPost("/api/orders/{id:int}/cancel", async (int id, CancelOrderRequest request, IMediator mediator) =>
{
    try
    {
        var command = new CancelOrderCommand
        {
            OrderId = id,
            CancellationReason = request.Reason
        };

        var result = await mediator.SendAsync(command);

        return result.Success
            ? Results.Ok(result)
            : Results.BadRequest(result);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new
        {
            errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
        });
    }
})
.WithName("CancelOrder")
.WithDescription("Cancel an order with reason");

// ========================================
// DEMO / TEST ENDPOINTS
// ========================================

app.MapGet("/", () => Results.Ok(new
{
    title = "TechnicalDogsbody.SimpleMediator Examples",
    description = "Demonstrates CQRS pattern with validation, logging, caching, and custom behaviors",
    version = "2.0.0",
    endpoints = new
    {
        products = new[]
        {
            "GET /api/products - Search products",
            "GET /api/products/{id} - Get product by ID (cached)"
        },
        orders = new[]
        {
            "POST /api/orders - Create new order (validated)",
            "GET /api/customers/{email}/orders - Get order history (cached)",
            "POST /api/orders/{id}/cancel - Cancel order (validated)"
        },
        admin = new[]
        {
            "PUT /api/products/{id}/price - Update product price (audited)"
        }
    },
    features = new[]
    {
        "✅ CQRS with Mediator pattern",
        "✅ FluentValidation integration",
        "✅ Request/response caching",
        "✅ Structured logging with LoggerMessage",
        "✅ Performance monitoring",
        "✅ Audit trail for commands",
        "✅ Automatic retry on transient failures",
        "✅ Zero-allocation static generic caching",
        "✅ Zero reflection - fully AOT compatible"
    }
}))
.WithName("Root")
.WithDescription("API information and available endpoints");

app.Run();

// ========================================
// REQUEST MODELS
// ========================================

namespace TechnicalDogsbody.MicroMediator.Examples
{
    internal record CancelOrderRequest
    {
        public required string Reason { get; init; }
    }
}
