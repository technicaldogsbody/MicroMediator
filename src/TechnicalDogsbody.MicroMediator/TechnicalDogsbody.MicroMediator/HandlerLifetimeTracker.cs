namespace TechnicalDogsbody.MicroMediator;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Tracks handler lifetimes for optimization purposes.
/// </summary>
internal static class HandlerLifetimeTracker
{
    private static readonly ConcurrentDictionary<Type, ServiceLifetime> _handlerLifetimes = new();

    /// <summary>
    /// Registers a handler type with its lifetime.
    /// </summary>
    public static void RegisterHandler(Type handlerInterfaceType, ServiceLifetime lifetime)
    {
        _handlerLifetimes.TryAdd(handlerInterfaceType, lifetime);
    }

    /// <summary>
    /// Gets the lifetime for a handler type. Returns null if not registered.
    /// </summary>
    public static ServiceLifetime? GetLifetime(Type handlerInterfaceType)
    {
        return _handlerLifetimes.TryGetValue(handlerInterfaceType, out var lifetime) ? lifetime : null;
    }
}
