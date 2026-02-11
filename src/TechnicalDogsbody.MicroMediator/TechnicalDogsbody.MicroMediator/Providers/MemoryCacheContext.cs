namespace TechnicalDogsbody.MicroMediator.Providers;

using Microsoft.Extensions.Caching.Memory;
using System;
using TechnicalDogsbody.MicroMediator.Abstractions;

internal class MemoryCacheContext : ICacheContext
{
    /// <summary>
    /// Gets or sets the absolute expiration time.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }

    /// <summary>
    /// Gets or sets the sliding expiration time.
    /// Entry will be removed if not accessed within this timespan.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the cache priority.
    /// Determines which entries are removed first under memory pressure.
    /// </summary>
    public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;
}
