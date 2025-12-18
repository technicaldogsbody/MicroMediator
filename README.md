# TechnicalDogsbody.MicroMediator

[![Build and Test](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/build-and-test.yml)
[![CodeQL](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/codeql.yml/badge.svg)](https://github.com/technicaldogsbody/MicroMediator/actions/workflows/codeql.yml)
[![NuGet](https://img.shields.io/nuget/v/TechnicalDogsbody.MicroMediator.svg)](https://www.nuget.org/packages/TechnicalDogsbody.MicroMediator/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight, high-performance mediator pattern implementation for .NET with built-in validation, logging, and caching pipeline behaviours. Zero commercial licensing concerns.

## Why MicroMediator?

MicroMediator outperforms MediatR across all metrics whilst maintaining a cleaner API and zero licensing costs.

### Performance Comparison vs MediatR 11

| Benchmark | MicroMediator | MediatR | Advantage |
|-----------|----------------|---------|-----------|
| **Basic Send** | 21 ns | 51 ns | **2.4x faster** |
| **Cold Start** | 95 μs | 396 μs | **4.2x faster** |
| **Pipeline (Validation)** | 380 ns | 867 ns | **2.3x faster** |
| **Throughput (10k sequential)** | 151 μs | 446 μs | **2.9x faster** |
| **Throughput (10k parallel)** | 287 μs | 617 μs | **2.1x faster** |

### Memory Efficiency

| Scenario | MicroMediator | MediatR | Savings |
|----------|----------------|---------|---------|
| **Single Request** | 96 B | 296 B | **3.1x less** |
| **100 Requests** | 2.41 KB | 21.59 KB | **9x less** |
| **10,000 Requests** | 234 KB | 2,187 KB | **9.3x less** |

### Key Features

- **2-4x faster** than MediatR across all scenarios
- **3-10x less memory allocation**
- **Zero commercial licensing costs** (MediatR 12+ requires paid licence)
- **Fluent builder API** for clean, readable configuration
- **Built-in behaviours**: Validation, Logging, Caching
- **ValueTask optimisation** for zero-allocation fast paths
- **AOT-compatible** with minimal reflection (registration only)
- **Cold start optimised** (4.2x faster than MediatR)

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

When you call `AddDefaultCachingPipeline()`, the `CachingBehavior` automatically caches responses for requests implementing `ICacheableRequest`.

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
- You need streaming request support

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
- Throughput at scale (100/1,000/10,000 requests)

## Requirements

- .NET 8.0 or later
- FluentValidation 11.10.0+ (optional, for validation support)

## Licence

MIT

## Contributing

Contributions welcome! Please open an issue before submitting large changes.

## Acknowledgements

Inspired by Jimmy Bogard's MediatR. Built from scratch with performance in mind.
