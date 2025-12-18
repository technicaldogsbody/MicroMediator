namespace TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Delegate for continuing the pipeline to the next behavior or handler.
/// Returns ValueTask to enable zero-allocation synchronous completions.
/// </summary>
/// <typeparam name="TResponse">The type of response.</typeparam>
/// <returns>A ValueTask containing the response from the next step in the pipeline.</returns>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Pipeline behavior for implementing cross-cutting concerns.
/// Order of execution is determined by registration order in DI container.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response being returned.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles a step in the pipeline.
    /// Can return synchronously completed ValueTask for fast-path scenarios (e.g., cache hits, validation failures).
    /// </summary>
    /// <param name="request">The request being processed.</param>
    /// <param name="next">Delegate to invoke the next behavior or handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask containing the response.</returns>
    ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
