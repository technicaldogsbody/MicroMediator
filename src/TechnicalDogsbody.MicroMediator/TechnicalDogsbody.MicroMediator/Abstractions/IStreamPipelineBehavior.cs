namespace TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Delegate for continuing the streaming pipeline to the next behavior or handler.
/// </summary>
/// <typeparam name="TResponse">The type of each item in the stream.</typeparam>
/// <returns>An async enumerable stream from the next step in the pipeline.</returns>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>();

/// <summary>
/// Pipeline behavior for streaming requests.
/// Operates on the entire stream rather than individual items.
/// </summary>
/// <typeparam name="TRequest">The type of streaming request being handled.</typeparam>
/// <typeparam name="TResponse">The type of each item in the response stream.</typeparam>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles a step in the streaming pipeline.
    /// Can wrap, filter, transform, or buffer the stream.
    /// </summary>
    /// <param name="request">The streaming request being processed.</param>
    /// <param name="next">Delegate to invoke the next behavior or handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable stream of responses.</returns>
    IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
