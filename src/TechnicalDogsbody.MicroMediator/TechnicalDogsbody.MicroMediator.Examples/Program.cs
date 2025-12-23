using FluentValidation;
using Scalar.AspNetCore;
using TechnicalDogsbody.MicroMediator;
using TechnicalDogsbody.MicroMediator.Abstractions;
using TechnicalDogsbody.MicroMediator.Examples.Behaviors;
using TechnicalDogsbody.MicroMediator.Examples.Commands;
using TechnicalDogsbody.MicroMediator.Examples.Queries;
using TechnicalDogsbody.MicroMediator.Examples.StreamingQueries;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

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
    // Streaming handlers
    .AddStreamHandler<ExportProductsByCategoryHandler>()
    .AddStreamHandler<StreamCustomerOrdersHandler>()
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
    app.MapScalarApiReference(options => options.WithTitle("MicroMediator Examples API")
               .WithTheme(ScalarTheme.Purple)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
    
    // Redirect root to Scalar UI
    app.MapGet("/", () => Results.Redirect("/scalar/v1"))
       .ExcludeFromDescription();
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
.WithSummary("Get product by ID")
.WithDescription("Retrieves a product by its ID. Results are cached for 10 minutes.");

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
.WithSummary("Search products")
.WithDescription("Search products with optional filters for category, price range, and stock status.");

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
.WithSummary("Get customer order history")
.WithDescription("Retrieves all orders for a customer. Results are cached for 5 minutes.");

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
.WithSummary("Create a new order")
.WithDescription("Creates a new order with validation for items and stock availability.");

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
.WithSummary("Update product price")
.WithDescription("Updates product price with optional reason. All price changes are audited.");

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
.WithSummary("Cancel an order")
.WithDescription("Cancels an order with a required reason. The operation is audited.");

// ========================================
// STREAMING ENDPOINTS
// ========================================

app.MapGet("/api/products/export", async (
    string? category,
    bool activeOnly,
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    var query = new ExportProductsByCategoryQuery
    {
        Category = category,
        ActiveOnly = activeOnly
    };

    var results = new List<object>();
    int count = 0;

    await foreach (var product in mediator.StreamAsync(query, cancellationToken))
    {
        count++;
        results.Add(new
        {
            product.Id,
            product.Name,
            product.Category,
            product.Price,
            product.StockQuantity
        });

        // Limit response size for demo purposes
        if (count >= 100)
        {
            results.Add(new { message = "... (truncated to 100 items for demo)" });
            break;
        }
    }

    return Results.Ok(new
    {
        category = category ?? "all",
        activeOnly,
        totalReturned = count,
        products = results
    });
})
.WithName("ExportProducts")
.WithSummary("Export products (streaming)")
.WithDescription("Exports products using IAsyncEnumerable streaming for memory efficiency. Supports filtering by category and active status.");

app.MapGet("/api/customers/{email}/orders/stream", async (
    string email,
    DateTime? fromDate,
    DateTime? toDate,
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    var query = new StreamCustomerOrdersQuery
    {
        CustomerEmail = email,
        FromDate = fromDate,
        ToDate = toDate
    };

    var orders = new List<object>();

    await foreach (var order in mediator.StreamAsync(query, cancellationToken))
    {
        orders.Add(new
        {
            order.Id,
            order.TotalAmount,
            order.Status,
            order.CreatedAt,
            itemCount = order.Items.Count
        });
    }

    return Results.Ok(new
    {
        customerEmail = email,
        orderCount = orders.Count,
        orders
    });
})
.WithName("StreamCustomerOrders")
.WithSummary("Stream customer orders")
.WithDescription("Streams customer orders with optional date filtering. Demonstrates efficient streaming of filtered results.");

app.Run();

// ========================================
// REQUEST MODELS
// ========================================

internal record CancelOrderRequest
{
    public required string Reason { get; init; }
}
