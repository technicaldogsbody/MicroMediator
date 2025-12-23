# TechnicalDogsbody.MicroMediator

[![Build and Test](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/build-and-test.yml)
[![CodeQL](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/codeql.yml/badge.svg)](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/codeql.yml)
[![NuGet](https://img.shields.io/nuget/v/TechnicalDogsbody.MicroMediator.svg)](https://www.nuget.org/packages/TechnicalDogsbody.MicroMediator/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight, high-performance mediator pattern implementation for .NET with built-in validation, logging, caching, and streaming support. Zero commercial licensing concerns.

## Why MicroMediator?

MicroMediator outperforms MediatR across all metrics whilst maintaining a cleaner API and zero licensing costs.

### Performance Comparison vs MediatR 11

| Benchmark | MicroMediator | MediatR | Advantage |
|-----------|----------------|---------|-----------|
| **Basic Send** | 23 ns | 55 ns | **2.4x faster** |
| **Cold Start** | 83 μs | 697 μs | **8.4x faster** |
| **Pipeline (Validation)** | 423 ns | 1000 ns | **2.4x faster** |
| **Streaming (100 items)** | 51 μs | N/A | Native support |
| **Streaming (1,000 items)** | 431 μs | N/A | Native support |
| **Streaming (5,000 items)** | 2.08 ms | N/A | Native support |
| **Throughput (10k sequential)** | 177 μs | 568 μs | **3.2x faster** |
| **Throughput (10k parallel)** | 340 μs | 748 μs | **2.2x faster** |

### Memory Efficiency

| Scenario | MicroMediator | MediatR | Savings |
|----------|----------------|---------|---------|
| **Single Request** | 96 B | 296 B | **3.1x less** |
| **100 Requests** | 2.41 KB | 21.59 KB | **9x less** |
| **10,000 Requests** | 234 KB | 2,187 KB | **9.3x less** |

### Key Features

- **2-8x faster** than MediatR across all scenarios
- **3-10x less memory allocation**
- **Zero commercial licensing costs** (MediatR 12+ requires paid licence)
- **Fluent builder API** for clean, readable configuration
- **Built-in behaviours**: Validation, Logging, Caching
- **Native streaming support** with `IAsyncEnumerable<T>`
- **ValueTask optimisation** for zero-allocation fast paths
- **AOT-compatible** with minimal reflection (registration only)
- **Cold start optimised** (8.4x faster than MediatR)

## Installation

```bash
dotnet add package TechnicalDogsbody.MicroMediator
```

## Quick Start

### 1. Define Your Request and Handler

```csharp
// Query (read operation)
public record GetProductByIdQuery : IRequest<Product?>
{
    public int Id { get; init; }
}

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Product?>
{
    public ValueTask<Product?> HandleAsync(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        // Your logic here
        var product = _repository.GetById(request.Id);
        return ValueTask.FromResult(product);
    }
}

// Command (write operation)
public record CreateOrderCommand : IRequest<OrderResult>
{
    public required string CustomerEmail { get; init; }
    public List<OrderItem> Items { get; init; } = [];
}

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderResult>
{
    public async ValueTask<OrderResult> HandleAsync(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Your logic here
        var orderId = await _orderService.CreateOrderAsync(request);
        return OrderResult.Success(orderId);
    }
}
```

### 2. Register with Dependency Injection

```csharp
builder.Services
    .AddMediator()
    .AddHandler<GetProductByIdQueryHandler>()
    .AddHandler<CreateOrderCommandHandler>()
    .AddDefaultLoggingPipeline()
    .AddDefaultCachingPipeline();
```

### 3. Use in Your Code

```csharp
public class ProductController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        var query = new GetProductByIdQuery { Id = id };
        var product = await _mediator.SendAsync(query);
        
        return product is not null 
            ? Ok(product) 
            : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderCommand command)
    {
        var result = await _mediator.SendAsync(command);
        return result.Success 
            ? Created($"/orders/{result.OrderId}", result)
            : BadRequest(result);
    }
}
```

## Advanced Features

### Streaming Requests

MicroMediator provides native support for streaming large datasets efficiently using `IAsyncEnumerable<T>`. This is perfect for scenarios like:

- Paginating large result sets
- Processing data that doesn't fit in memory
- Real-time data feeds
- Exporting large reports

#### Basic Streaming

```csharp
// Define a streaming request
public record StreamProductsQuery : IStreamRequest<Product>
{
    public string? CategoryFilter { get; init; }
    public decimal? MinPrice { get; init; }
}

// Handler yields results as they're retrieved
public class StreamProductsQueryHandler : IStreamRequestHandler<StreamProductsQuery, Product>
{
    private readonly IProductRepository _repository;

    public async IAsyncEnumerable<Product> HandleAsync(
        StreamProductsQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var product in _repository.StreamAllAsync(cancellationToken))
        {
            if (request.CategoryFilter != null && product.Category != request.CategoryFilter)
                continue;
                
            if (request.MinPrice.HasValue && product.Price < request.MinPrice)
                continue;
                
            yield return product;
        }
    }
}

// Usage - process items as they arrive
var query = new StreamProductsQuery { MinPrice = 50 };

await foreach (var product in _mediator.CreateStream(query))
{
    Console.WriteLine($"{product.Name}: {product.Price:C}");
    // Process each product immediately without loading entire result set
}
```

#### Early Exit with Streaming

Streaming allows efficient early termination:

```csharp
// Take first 50 matching products, then stop
var expensiveProducts = _mediator
    .CreateStream(new StreamProductsQuery { MinPrice = 1000 })
    .Take(50);

await foreach (var product in expensiveProducts)
{
    // Only 50 products processed, even if database has millions
}
```

#### Performance Characteristics

**Memory Usage**: Streaming keeps memory footprint constant regardless of result size:

```csharp
// Traditional: Loads all 5,000 products into memory (~1.5 MB)
var allProducts = await _mediator.SendAsync(new GetAllProductsQuery());
var total = allProducts.Sum(p => p.Price);

// Streaming: Processes one at a time (~30 KB peak memory)
var total = 0m;
await foreach (var product in _mediator.CreateStream(new StreamProductsQuery()))
{
    total += product.Price;
}
```

**Benchmark Results** (processing 5,000 items):

| Operation | Load All | Stream ToList | Stream Process | Stream Take(50) |
|-----------|----------|---------------|----------------|------------------|
| Time | 1.66 ms | 2.08 ms | 2.08 ms | 29.2 μs |
| Memory | 1,485 KB | 1,485 KB | 1,446 KB | 14.8 KB |

Key insights:
- **Early exit**: 57x faster when you don't need all results
- **Memory efficiency**: 50-100x less memory for processing-only scenarios
- **Minimal overhead**: Only ~25% slower when collecting all results

### Fluent Configuration

```csharp
builder.Services
    .AddMediator()
    // Register handlers (discovers interfaces automatically)
    .AddHandler<GetProductByIdQueryHandler>()
    .AddHandler<SearchProductsQueryHandler>()
    .AddHandler<CreateOrderCommandHandler>()
    
    // Register validators (automatically adds ValidationBehavior)
    .AddValidator<CreateOrderCommandValidator>()
    .AddValidator<UpdateProductCommandValidator>()
    
    // Add built-in pipeline behaviours
    .AddDefaultLoggingPipeline()
    .AddDefaultCachingPipeline()
    
    // Add custom pipeline behaviours
    .AddBehavior(typeof(PerformanceMonitoringBehavior<,>))
    .AddBehavior(typeof(AuditBehavior<,>))
    .AddBehavior(typeof(RetryBehavior<,>));
```

### Request Caching

Implement `ICacheableRequest` on your queries for automatic caching:

```csharp
public record GetProductByIdQuery : IRequest<Product?>, ICacheableRequest
{
    public int Id { get; init; }
    
    public string CacheKey => $"product-{Id}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}
```

#### Default Memory Cache

When you call `AddDefaultCachingPipeline()`, the `CachingBehavior` automatically caches responses using `IMemoryCache`:

```csharp
builder.Services
    .AddMediator()
    .AddHandler<GetProductByIdQueryHandler>()
    .AddDefaultCachingPipeline(); // Uses IMemoryCache
```

#### Custom Cache Providers

MicroMediator's caching is extensible through the `ICacheProvider` abstraction. Swap in FusionCache, Redis, or any custom implementation:

```csharp
// Use FusionCache
builder.Services
    .AddMediator()
    .AddCachingPipeline<FusionCacheProvider>();

// Or register manually
builder.Services.AddSingleton<ICacheProvider, RedisCacheProvider>();
builder.Services
    .AddMediator()
    .AddBehavior(typeof(CachingBehavior<,>));
```

**Example: FusionCache Provider**

```csharp
public class FusionCacheProvider : ICacheProvider
{
    private readonly IFusionCache _cache;
    
    public FusionCacheProvider(IFusionCache cache)
    {
        _cache = cache;
    }
    
    public bool TryGet<TResponse>(string cacheKey, out TResponse? value)
    {
        var result = _cache.TryGet<TResponse>(cacheKey);
        if (result.HasValue)
        {
            value = result.Value;
            return true;
        }
        
        value = default;
        return false;
    }
    
    public void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration)
    {
        _cache.Set(cacheKey, value, duration);
    }
}
```

**Example: Redis Distributed Cache Provider**

```csharp
public class RedisCacheProvider : ICacheProvider
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheProvider> _logger;
    
    public RedisCacheProvider(IDistributedCache cache, ILogger<RedisCacheProvider> logger)
    {
        _cache = cache;
        _logger = logger;
    }
    
    public bool TryGet<TResponse>(string cacheKey, out TResponse? value)
    {
        try
        {
            var bytes = _cache.Get(cacheKey);
            if (bytes != null)
            {
                value = JsonSerializer.Deserialize<TResponse>(bytes);
                return value != null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from Redis: {CacheKey}", cacheKey);
        }
        
        value = default;
        return false;
    }
    
    public void Set<TResponse>(string cacheKey, TResponse value, TimeSpan duration)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration
            };
            _cache.Set(cacheKey, bytes, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing in Redis: {CacheKey}", cacheKey);
        }
    }
}
```

### FluentValidation Integration

Add validators and MicroMediator automatically wires up validation:

```csharp
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Items)
            .NotEmpty()
            .Must(items => items.Count <= 50);
    }
}

// Registration
builder.Services
    .AddMediator()
    .AddHandler<CreateOrderCommandHandler>()
    .AddValidator<CreateOrderCommandValidator>(); // Automatically adds ValidationBehavior
```

### Custom Pipeline Behaviours

Create custom behaviours by implementing `IPipelineBehavior<TRequest, TResponse>`:

```csharp
public class PerformanceMonitoringBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceMonitoringBehavior<TRequest, TResponse>> _logger;

    public PerformanceMonitoringBehavior(ILogger<PerformanceMonitoringBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            return await next();
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > 500)
            {
                _logger.LogWarning(
                    "Slow request: {RequestName} took {ElapsedMs}ms",
                    typeof(TRequest).Name,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
}

// Register it
builder.Services
    .AddMediator()
    .AddBehavior(typeof(PerformanceMonitoringBehavior<,>));
```

## Pipeline Execution Order

Behaviours execute in **reverse order of registration** (last registered runs first):

```csharp
builder.Services
    .AddMediator()
    .AddHandler<MyHandler>()
    .AddValidator<MyValidator>()      // 3. Validates (innermost)
    .AddDefaultLoggingPipeline()      // 2. Logs
    .AddBehavior(typeof(RetryBehavior<,>)); // 1. Retries (outermost)
```

Request flow: `RetryBehavior` → `LoggingBehavior` → `ValidationBehavior` → `Handler`

## Explicit Registration (Zero Reflection)

For maximum AOT compatibility and performance, use explicit type parameters:

```csharp
builder.Services
    .AddMediator()
    .AddHandler<GetProductQuery, Product, GetProductQueryHandler>()
    .AddValidator<CreateOrderCommand, CreateOrderCommandValidator>();
```

## Use Cases

### Perfect For

- **Serverless/Azure Functions** - 4.2x faster cold starts
- **High-throughput APIs** - 2-3x faster request processing
- **Memory-constrained environments** - 9x less allocation
- **CQRS implementations** - Clean separation of commands and queries
- **Projects avoiding commercial licences** - MediatR 12+ requires payment

### When to Use MediatR Instead

- You need notification/event broadcasting (MicroMediator focuses on request/response)
- You're already using MediatR 11 and don't want to migrate

## Architecture

MicroMediator uses a wrapper pattern with dynamic dispatch and aggressive caching:

1. **Static generic caching** - Each response type gets its own dictionary
2. **ConcurrentDictionary** - Lock-free reads after first request
3. **Wrapper instances** - Created once per request type
4. **Handler/behavior caching** - Eliminates DI lookups on hot path
5. **ValueTask** - Zero-allocation synchronous completions

## Examples

The `TechnicalDogsbody.MicroMediator.Examples` project demonstrates:

- CQRS pattern with queries and commands
- FluentValidation integration
- Request caching
- Streaming large datasets with `IAsyncEnumerable<T>`
- Custom pipeline behaviours (performance monitoring, audit trail, retry logic)
- Structured logging
- Complete Web API implementation

## Benchmarks

Run comprehensive benchmarks comparing MicroMediator to MediatR:

```bash
cd benchmarks
dotnet run -c Release
```

Includes:
- Basic send performance
- Pipeline overhead with validation
- Caching performance (hit/miss)
- Cold start performance
- Streaming performance (100/1,000/5,000/10,000 items)
- Early exit scenarios
- Throughput at scale (100/1,000/10,000 requests)
- Registration strategies (explicit vs reflection)

## Requirements

- .NET 8.0 or later
- FluentValidation 11.10.0+ (optional, for validation support)

## Licence

MIT

## Contributing

Contributions welcome! Please open an issue before submitting large changes.

## Acknowledgements

Inspired by Jimmy Bogard's MediatR. Built from scratch with performance in mind.
