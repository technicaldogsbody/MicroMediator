namespace TechnicalDogsbody.MicroMediator.Tests.Providers;

using Microsoft.Extensions.Caching.Memory;
using TechnicalDogsbody.MicroMediator.Providers;

public class MemoryCacheProviderTests
{
    [Fact]
    public void Constructor_WithValidCache_Succeeds()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new MemoryCacheProvider(cache);

        Assert.NotNull(provider);
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(() => new MemoryCacheProvider(null!));

    [Fact]
    public void TryGet_WithNonExistentKey_ReturnsFalse()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new MemoryCacheProvider(cache);

        bool result = provider.TryGet<string>("nonexistent", out string? value);

        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryGet_WithExistingKey_ReturnsTrueAndValue()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new MemoryCacheProvider(cache);

        provider.Set("test-key", "test-value", TimeSpan.FromMinutes(5));

        bool result = provider.TryGet<string>("test-key", out string? value);

        Assert.True(result);
        Assert.Equal("test-value", value);
    }

    [Fact]
    public void TryGet_AfterExpiration_ReturnsFalse()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new MemoryCacheProvider(cache);

        provider.Set("test-key", "test-value", TimeSpan.FromMilliseconds(10));
        Thread.Sleep(50);

        bool result = provider.TryGet<string>("test-key", out string? value);

        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void Set_WithValidParameters_StoresValue()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new MemoryCacheProvider(cache);

        provider.Set("test-key", "test-value", TimeSpan.FromMinutes(5));

        Assert.True(cache.TryGetValue("test-key", out string? value));
        Assert.Equal("test-value", value);
    }

    [Fact]
    public void Set_WithComplexObject_StoresAndRetrievesCorrectly()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new MemoryCacheProvider(cache);
        var testObject = new TestData { Id = 123, Name = "Test" };

        provider.Set("complex-key", testObject, TimeSpan.FromMinutes(5));

        bool result = provider.TryGet<TestData>("complex-key", out var retrievedObject);

        Assert.True(result);
        Assert.NotNull(retrievedObject);
        Assert.Equal(123, retrievedObject.Id);
        Assert.Equal("Test", retrievedObject.Name);
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new MemoryCacheProvider(cache);

        provider.Set("test-key", "first-value", TimeSpan.FromMinutes(5));
        provider.Set("test-key", "second-value", TimeSpan.FromMinutes(5));

        bool result = provider.TryGet<string>("test-key", out string? value);

        Assert.True(result);
        Assert.Equal("second-value", value);
    }

    [Fact]
    public void Set_WithDifferentTypes_StoresIndependently()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new MemoryCacheProvider(cache);

        provider.Set("string-key", "string-value", TimeSpan.FromMinutes(5));
        provider.Set("int-key", 42, TimeSpan.FromMinutes(5));

        bool stringResult = provider.TryGet<string>("string-key", out string? stringValue);
        bool intResult = provider.TryGet<int>("int-key", out int intValue);

        Assert.True(stringResult);
        Assert.Equal("string-value", stringValue);
        Assert.True(intResult);
        Assert.Equal(42, intValue);
    }

    [Fact]
    public void TryGet_WithNullValue_ReturnsTrue()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new MemoryCacheProvider(cache);

        provider.Set<string?>("null-key", null, TimeSpan.FromMinutes(5));

        bool result = provider.TryGet<string?>("null-key", out string? value);

        Assert.True(result);
        Assert.Null(value);
    }

    [Fact]
    public void Set_WithVeryShortDuration_ExpiresQuickly()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new MemoryCacheProvider(cache);

        provider.Set("test-key", "test-value", TimeSpan.FromMilliseconds(1));
        Thread.Sleep(50);

        bool result = provider.TryGet<string>("test-key", out string? value);

        Assert.False(result);
        Assert.Null(value);
    }

    [ExcludeFromCodeCoverage]
    private class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
