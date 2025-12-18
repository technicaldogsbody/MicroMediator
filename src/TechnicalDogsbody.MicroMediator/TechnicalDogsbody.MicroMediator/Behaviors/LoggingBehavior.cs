using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TechnicalDogsbody.MicroMediator.Abstractions;

namespace TechnicalDogsbody.MicroMediator.Behaviors;

/// <summary>
/// Logs request execution with timing and exception details.
/// Register after validation but before caching for complete observability.
/// </summary>
/// <typeparam name="TRequest">The type of request being logged.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed partial class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initialises a new instance of the LoggingBehavior class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        LogHandlingRequest(_logger, requestName);

        try
        {
            var response = await next();

            stopwatch.Stop();

            LogHandledRequest(_logger, requestName, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            LogHandlingError(_logger, requestName, stopwatch.ElapsedMilliseconds, ex);

            throw;
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Handling {RequestName}")]
    static partial void LogHandlingRequest(ILogger logger, string requestName);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Handled {RequestName} in {ElapsedMilliseconds}ms")]
    static partial void LogHandledRequest(ILogger logger, string requestName, long elapsedMilliseconds);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Error handling {RequestName} after {ElapsedMilliseconds}ms")]
    static partial void LogHandlingError(ILogger logger, string requestName, long elapsedMilliseconds, Exception exception);
}
