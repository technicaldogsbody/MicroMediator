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
}
