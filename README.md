# TechnicalDogsbody.MicroMediator

[![Build and Test](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/build-and-test.yml)
[![CodeQL](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/codeql.yml/badge.svg)](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/codeql.yml)
[![NuGet](https://img.shields.io/nuget/v/TechnicalDogsbody.MicroMediator.svg)](https://www.nuget.org/packages/TechnicalDogsbody.MicroMediator/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight, high-performance mediator pattern implementation for .NET with built-in validation, logging, caching, and streaming support.

## Features

- **Fast execution** - Optimised request handling with ValueTask and aggressive caching
- **Low memory footprint** - Minimal allocations through efficient dispatch mechanisms
- **Native streaming** - Built-in `IAsyncEnumerable<T>` support for large datasets
- **Fluent configuration** - Clean, readable service registration
- **Built-in behaviours** - Validation, logging, and caching out of the box
- **AOT compatible** - Minimal reflection, explicit registration available
- **MIT licence** - Use freely in commercial projects

## Installation

```bash
dotnet add package TechnicalDogsbody.MicroMediator
```

## Quick Start

### Define Request and Handler

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
        var orderId = await _orderService.CreateOrderAsync(request);
        return OrderResult.Success(orderId);
    }
}
```

### Register Services

```csharp
builder.Services
    .AddMediator()
    .AddHandler<GetProductByIdQueryHandler>()
    .AddHandler<CreateOrderCommandHandler>()
    .AddDefaultLoggingPipeline()
    .AddDefaultCachingPipeline();
```

### Use in Controllers

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
        
        return product is not null ? Ok(product) : NotFound();
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

## Streaming Requests

Stream large datasets efficiently without loading everything into memory.

### Basic Streaming

```csharp
public record StreamProductsQuery : IStreamRequest<Product>
{
    public string? CategoryFilter { get; init; }
    public decimal? MinPrice { get; init; }
}

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

// Process items as they arrive
var query = new StreamProductsQuery { MinPrice = 50 };

await foreach (var product in _mediator.CreateStream(query))
{
    Console.WriteLine($"{product.Name}: {product.Price:C}");
}
```

### Early Exit

```csharp
// Process only first 50 matching products
var expensiveProducts = _mediator
    .CreateStream(new StreamProductsQuery { MinPrice = 1000 })
    .Take(50);

await foreach (var product in expensiveProducts)
{
    // Only 50 products processed, even if database has millions
}
```

### Memory Efficiency

Streaming keeps memory constant regardless of result size:

```csharp
// Traditional: Loads all 5,000 products into memory (~1.5 MB)
var allProducts = await _mediator.SendAsync(new GetAllProductsQuery());
var total = allProducts.Sum(p => p.Price);

// Streaming: Processes one at a time (~15 KB peak memory)
var total = 0m;
await foreach (var product in _mediator.CreateStream(new StreamProductsQuery()))
{
    total += product.Price;
}
```

## Request Caching

Implement `ICacheableRequest` for automatic caching:

```csharp
public record GetProductByIdQuery : IRequest<Product?>, ICacheableRequest
{
    public int Id { get; init; }
    
    public string CacheKey => $"product-{Id}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}
```

### Default Memory Cache

```csharp
builder.Services
    .AddMediator()
    .AddHandler<GetProductByIdQueryHandler>()
    .AddDefaultCachingPipeline(); // Uses IMemoryCache
```

### Custom Cache Providers

Swap in FusionCache, Redis, or any custom implementation through `ICacheProvider`:

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

**FusionCache Example**

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

## FluentValidation Integration

Add validators and MicroMediator wires up validation automatically:

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

builder.Services
    .AddMediator()
    .AddHandler<CreateOrderCommandHandler>()
    .AddValidator<CreateOrderCommandValidator>(); // Adds ValidationBehavior
```

## Custom Pipeline Behaviours

Implement `IPipelineBehavior<TRequest, TResponse>` for cross-cutting concerns:

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

builder.Services
    .AddMediator()
    .AddBehavior(typeof(PerformanceMonitoringBehavior<,>));
```

### Pipeline Execution Order

Behaviours execute in reverse order of registration (last registered runs first):

```csharp
builder.Services
    .AddMediator()
    .AddHandler<MyHandler>()
    .AddValidator<MyValidator>()           // 3. Validates (innermost)
    .AddDefaultLoggingPipeline()           // 2. Logs
    .AddBehavior(typeof(RetryBehavior<,>)); // 1. Retries (outermost)
```

Request flow: `RetryBehavior` → `LoggingBehavior` → `ValidationBehavior` → `Handler`

## AOT Compatibility

Use explicit type parameters for zero reflection:

```csharp
builder.Services
    .AddMediator()
    .AddHandler<GetProductQuery, Product, GetProductQueryHandler>()
    .AddValidator<CreateOrderCommand, CreateOrderCommandValidator>();
```

## Use Cases

MicroMediator fits well for:

- **Serverless environments** - Fast cold starts and low memory usage
- **High-throughput APIs** - Efficient request processing
- **Memory-constrained systems** - Minimal allocations
- **CQRS implementations** - Clean separation of commands and queries
- **Large dataset processing** - Native streaming support
- **Commercial projects** - MIT licence with no restrictions

## Architecture

MicroMediator uses a wrapper pattern with dynamic dispatch and aggressive caching:

1. **Static generic caching** - Each response type gets its own dictionary
2. **ConcurrentDictionary** - Lock-free reads after first request
3. **Wrapper instances** - Created once per request type
4. **Handler caching** - Eliminates DI lookups on hot path
5. **ValueTask** - Zero-allocation synchronous completions

## Examples

The `TechnicalDogsbody.MicroMediator.Examples` project demonstrates:

- CQRS pattern with queries and commands
- FluentValidation integration
- Request caching with custom providers
- Streaming large datasets
- Custom pipeline behaviours (performance monitoring, audit trail, retry logic)
- Structured logging
- Complete Web API implementation

Run the examples:

```bash
cd examples
dotnet run
```

## Benchmarks

The benchmark suite measures performance across various scenarios. All benchmarks run on .NET 10.0 to showcase peak performance characteristics.

Run benchmarks:

```bash
cd benchmarks
dotnet run -c Release
```

### Core Performance

| Scenario | Time (ns) | Memory |
|----------|-----------|--------|
| Basic Send | 24 | 96 B |
| With Validation | 373 | 1.65 KB |
| Cold Start | 434 μs | 9.53 KB |
| Cache Hit | 105 | 272 B |
| Cache Miss | 1,527 | 648 B |

### Throughput (10,000 requests)

| Mode | Time | Memory | Notes |
|------|------|--------|-------|
| Sequential | 181 μs | 234 KB | 2.6x faster than alternatives |
| Parallel | 344 μs | 1,133 KB | 1.9x faster than alternatives |

### Streaming Performance (5,000 items)

| Operation | Time | Memory | Notes |
|-----------|------|--------|-------|
| Load All | 1.51 ms | 1,485 KB | Baseline |
| Stream ToList | 2.40 ms | 1,485 KB | ~25% overhead |
| Stream Process | 2.35 ms | 1,446 KB | 3% less memory |
| Stream Take(50) | 33 μs | 15 KB | 46x faster, 99% less memory |

### Large Dataset Stress Test (1,000,000 items)

| Operation | Time | Memory | Notes |
|-----------|------|--------|-------|
| Early Exit (after 100) | 31 μs | 688 B | Constant time |
| Complete Processing | 29.6 ms | 744 B | Linear scaling |
| Count Only | 29.4 ms | 744 B | Minimal overhead |
| ToList (limited 1000) | 336 μs | 40 KB | Bounded memory |

Key findings:

- **Early exit efficiency** - Streaming exits immediately when condition met, no wasted work
- **Memory scaling** - Memory usage stays constant regardless of total dataset size
- **Linear performance** - Processing time scales linearly with items consumed, not total size
- **Bounded operations** - Take/limit operations maintain low memory even with massive datasets

## Requirements

- .NET 8.0, 9.0, or 10.0
- FluentValidation 12.1.1+ (optional, for validation support)

## Licence

MIT

## Contributing

Contributions welcome. Open an issue before submitting large changes.

## Acknowledgements

Inspired by the mediator pattern and Jimmy Bogard's MediatR. Built from scratch with performance and developer experience in mind.