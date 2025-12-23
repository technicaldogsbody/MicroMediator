namespace TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Main mediator interface for sending requests to handlers.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Sends a request through the mediator pipeline to its handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask containing the response from the handler.</returns>
    ValueTask<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a streaming request to its handler.
    /// Returns items as they become available rather than waiting for the entire result set.
    /// </summary>
    /// <typeparam name="TResponse">The type of each item in the stream.</typeparam>
    /// <param name="request">The streaming request to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable stream of responses.</returns>
    IAsyncEnumerable<TResponse> StreamAsync<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
