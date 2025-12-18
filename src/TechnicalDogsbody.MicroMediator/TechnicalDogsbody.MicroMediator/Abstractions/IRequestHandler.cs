namespace TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Handler interface for processing requests.
/// </summary>
/// <typeparam name="TRequest">The type of request to handle.</typeparam>
/// <typeparam name="TResponse">The type of response to return.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request asynchronously.
    /// Implementers can return synchronously completed ValueTask for fast-path scenarios.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask containing the response.</returns>
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
