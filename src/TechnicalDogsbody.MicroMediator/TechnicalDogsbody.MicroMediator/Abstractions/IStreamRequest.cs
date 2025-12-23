namespace TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Marker interface for streaming requests that return IAsyncEnumerable.
/// Use for large result sets where loading everything into memory would be wasteful.
/// </summary>
/// <typeparam name="TResponse">The type of each item in the stream.</typeparam>
public interface IStreamRequest<out TResponse>
{
}
