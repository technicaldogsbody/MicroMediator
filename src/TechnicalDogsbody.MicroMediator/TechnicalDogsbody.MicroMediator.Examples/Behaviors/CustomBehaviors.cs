
namespace TechnicalDogsbody.MicroMediator.Examples.Behaviors;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Custom behavior that tracks performance metrics for requests
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
[ExcludeFromCodeCoverage]
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
        string requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            // Log warning if request takes longer than threshold
            if (stopwatch.ElapsedMilliseconds > 500)
            {
                _logger.LogWarning(
                    "?? SLOW REQUEST: {RequestName} took {ElapsedMilliseconds}ms (threshold: 500ms)",
                    requestName,
                    stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            _logger.LogError(
                "? REQUEST FAILED: {RequestName} failed after {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>
/// Custom behavior that adds audit trail for commands
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
[ExcludeFromCodeCoverage]
public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
where TRequest : IRequest<TResponse>
{
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    public AuditBehavior(ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;

        // Only audit commands (requests that end with "Command")
        if (requestName.EndsWith("Command"))
        {
            _logger.LogInformation(
                "?? AUDIT: Executing command {CommandName} at {Timestamp}",
                requestName,
                DateTime.UtcNow);

            // In a real application, you would save this to an audit log database
            var auditEntry = new AuditEntry
            {
                CommandName = requestName,
                ExecutedAt = DateTime.UtcNow,
                RequestData = System.Text.Json.JsonSerializer.Serialize(request)
            };

            // Simulate saving to audit log
            await Task.Delay(10, cancellationToken);
        }

        return await next();
    }

    private class AuditEntry
    {
        public required string CommandName { get; init; }
        public DateTime ExecutedAt { get; init; }
        public required string RequestData { get; init; }
    }
}

/// <summary>
/// Custom behavior for retry logic on transient failures
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
[ExcludeFromCodeCoverage]
public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
where TRequest : IRequest<TResponse>
{
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;
    private const int MaxRetries = 3;
    private const int DelayMilliseconds = 100;

    public RetryBehavior(ILogger<RetryBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;
        int attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                return await next();
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < MaxRetries)
            {
                _logger.LogWarning(
                    "?? RETRY: {RequestName} failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms. Error: {Error}",
                    requestName,
                    attempt,
                    MaxRetries,
                    DelayMilliseconds,
                    ex.Message);

                await Task.Delay(DelayMilliseconds * attempt, cancellationToken);
            }
        }
    }

    private static bool IsTransientError(Exception ex)
    {
        // In a real application, you would check for specific transient errors
        // like TimeoutException, network errors, database deadlocks, etc.
        return ex is TimeoutException or InvalidOperationException;
    }
}