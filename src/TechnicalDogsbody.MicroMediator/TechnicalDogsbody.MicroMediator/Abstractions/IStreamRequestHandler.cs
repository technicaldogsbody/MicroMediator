namespace TechnicalDogsbody.MicroMediator.Abstractions;

using System.Runtime.CompilerServices;

/// <summary>
/// Handler interface for processing streaming requests.
/// Returns IAsyncEnumerable for efficient streaming of large result sets.
/// </summary>
/// <typeparam name="TRequest">The type of streaming request to handle.</typeparam>
/// <typeparam name="TResponse">The type of each item in the response stream.</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the streaming request asynchronously.
    /// Use yield return to emit items as they become available.
    /// </summary>
    /// <param name="request">The streaming request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable stream of responses.</returns>
    IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken);
}
