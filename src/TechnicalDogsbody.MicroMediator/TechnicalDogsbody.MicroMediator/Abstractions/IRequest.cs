namespace TechnicalDogsbody.MicroMediator.Abstractions;

/// <summary>
/// Marker interface for all requests (commands and queries).
/// </summary>
/// <typeparam name="TResponse">The type of response returned by this request.</typeparam>
public interface IRequest<out TResponse>
{
}
