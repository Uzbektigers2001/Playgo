using FluentAssertions;
using Playgo.Application.Common.Interfaces;

namespace Playgo.Tests;

public class CacheServiceTests
{
    /// <summary>
    /// In-memory ICacheService double — exercises the contract used by services.
    /// Real RedisCacheService is integration-tested via dotnet run.
    /// </summary>
    private sealed class InMemoryCache : ICacheService
    {
        private readonly Dictionary<string, object> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            => Task.FromResult(_store.TryGetValue(key, out var v) ? v as T : null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
        {
            _store[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
        {
            var matched = _store.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            foreach (var k in matched) _store.Remove(k);
            return Task.CompletedTask;
        }
    }

    private sealed class Box { public string? Value { get; set; } }

    [Fact]
    public async Task SetAndGet_RoundTrips()
    {
        ICacheService cache = new InMemoryCache();
        await cache.SetAsync("k1", new Box { Value = "v1" });
        var fetched = await cache.GetAsync<Box>("k1");
        fetched.Should().NotBeNull();
        fetched!.Value.Should().Be("v1");
    }

    [Fact]
    public async Task RemoveByPrefix_RemovesMatchingOnly()
    {
        ICacheService cache = new InMemoryCache();
        await cache.SetAsync("content:trending:1", new Box { Value = "a" });
        await cache.SetAsync("content:featured:1", new Box { Value = "b" });
        await cache.SetAsync("genres:all", new Box { Value = "c" });

        await cache.RemoveByPrefixAsync("content:");

        (await cache.GetAsync<Box>("content:trending:1")).Should().BeNull();
        (await cache.GetAsync<Box>("content:featured:1")).Should().BeNull();
        (await cache.GetAsync<Box>("genres:all")).Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveAsync_RemovesSingleKey()
    {
        ICacheService cache = new InMemoryCache();
        await cache.SetAsync("user:prefs:abc", new Box { Value = "v" });
        await cache.RemoveAsync("user:prefs:abc");
        (await cache.GetAsync<Box>("user:prefs:abc")).Should().BeNull();
    }
}
