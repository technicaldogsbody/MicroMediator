namespace TechnicalDogsbody.MicroMediator.Tests;

using Microsoft.Extensions.DependencyInjection;
using TechnicalDogsbody.MicroMediator.Abstractions;

public class StreamingTests
{
    [Fact]
    public async Task StreamAsync_WithSimpleHandler_ReturnsAllItems()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator()
            .AddStreamHandler<SimpleStreamQuery, int, SimpleStreamHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var query = new SimpleStreamQuery { Count = 10 };

        // Act
        var results = await mediator.StreamAsync(query).ToListAsync();

        // Assert
        Assert.Equal(10, results.Count);
        Assert.Equal(Enumerable.Range(1, 10), results);
    }

    [Fact]
    public async Task StreamAsync_WithCancellation_StopsStreaming()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator()
            .AddStreamHandler<SimpleStreamQuery, int, SimpleStreamHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var query = new SimpleStreamQuery { Count = 1000 };
        var cts = new CancellationTokenSource();

        // Act
        var results = new List<int>();
        var exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (int item in mediator.StreamAsync(query, cts.Token))
            {
                results.Add(item);
                if (results.Count >= 5)
                {
                    cts.Cancel();
                }
            }
        });

        // Assert
        Assert.IsType<OperationCanceledException>(exception);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task StreamAsync_WithFilter_ReturnsFilteredItems()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator()
            .AddStreamHandler<FilteredStreamQuery, int, FilteredStreamHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var query = new FilteredStreamQuery { Count = 20, OnlyEven = true };

        // Act
        var results = new List<int>();
        await foreach (int item in mediator.StreamAsync(query))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(10, results.Count);
        Assert.All(results, item => Assert.Equal(0, item % 2));
    }

    [Fact]
    public async Task StreamAsync_WithBehavior_ExecutesPipelineCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator()
            .AddStreamHandler<SimpleStreamQuery, int, SimpleStreamHandler>()
            .AddStreamBehavior(typeof(CountingStreamBehavior<,>));

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var query = new SimpleStreamQuery { Count = 5 };

        // Act
        var results = new List<int>();
        await foreach (int item in mediator.StreamAsync(query))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(5, results.Count);
        // Behavior doubles each value
        Assert.Equal([2, 4, 6, 8, 10], results);
    }

    [Fact]
    public async Task StreamAsync_WithNoHandler_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var query = new SimpleStreamQuery { Count = 10 };

        // Act & Assert
        // Exception is thrown when we try to enumerate, not when we call StreamAsync
        var stream = mediator.StreamAsync(query);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (int item in stream)
            {
                // Should throw before returning any items
            }
        });

        Assert.Contains("No handler registered", exception.Message);
        Assert.Contains("SimpleStreamQuery", exception.Message);
    }

    [Fact]
    public async Task StreamAsync_WithEmptyResults_ReturnsEmptyStream()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator()
            .AddStreamHandler<SimpleStreamQuery, int, SimpleStreamHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var query = new SimpleStreamQuery { Count = 0 };

        // Act
        var results = new List<int>();
        await foreach (int item in mediator.StreamAsync(query))
        {
            results.Add(item);
        }

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task StreamAsync_CalledMultipleTimes_UsesCache()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediator()
            .AddStreamHandler<SimpleStreamQuery, int, SimpleStreamHandler>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var query = new SimpleStreamQuery { Count = 3 };

        // Act - Call multiple times
        var firstResults = await mediator.StreamAsync(query).ToListAsync();
        var secondResults = await mediator.StreamAsync(query).ToListAsync();

        // Assert
        Assert.Equal(firstResults, secondResults);
        Assert.Equal([1, 2, 3], firstResults);
    }
}

// Test request types
public record SimpleStreamQuery : IStreamRequest<int>
{
    public int Count { get; init; }
}

public record FilteredStreamQuery : IStreamRequest<int>
{
    public int Count { get; init; }
    public bool OnlyEven { get; init; }
}

// Test handlers
public class SimpleStreamHandler : IStreamRequestHandler<SimpleStreamQuery, int>
{
    public async IAsyncEnumerable<int> HandleAsync(
        SimpleStreamQuery request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return i;
        }
    }
}

public class FilteredStreamHandler : IStreamRequestHandler<FilteredStreamQuery, int>
{
    public async IAsyncEnumerable<int> HandleAsync(
        FilteredStreamQuery request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.OnlyEven && i % 2 != 0)
            {
                continue;
            }

            await Task.Yield();
            yield return i;
        }
    }
}

// Test behavior that doubles each streamed value
public class CountingStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
    where TResponse : struct
{
    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in next().WithCancellation(cancellationToken))
        {
            // Double the value (assumes TResponse is numeric)
            if (item is int intValue)
            {
                yield return (TResponse)(object)(intValue * 2);
            }
            else
            {
                yield return item;
            }
        }
    }
}
