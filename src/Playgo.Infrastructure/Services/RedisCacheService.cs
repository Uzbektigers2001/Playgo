using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Playgo.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Playgo.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly string _instanceName;

    public RedisCacheService(
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _redis = redis;
        _logger = logger;
        _instanceName = configuration["Redis:InstanceName"] ?? "playgo:";
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var bytes = await _cache.GetAsync(key, ct);
            if (bytes is null || bytes.Length == 0) return null;
            return JsonSerializer.Deserialize<T>(bytes, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);
            var options = new DistributedCacheEntryOptions();
            if (expiry.HasValue)
                options.AbsoluteExpirationRelativeToNow = expiry.Value;
            await _cache.SetAsync(key, bytes, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for {Key}", key);
        }
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            var endpoints = _redis.GetEndPoints();
            var fullPrefix = _instanceName + prefix;
            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica) continue;

                foreach (var key in server.Keys(pattern: fullPrefix + "*"))
                {
                    _redis.GetDatabase().KeyDelete(key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE-BY-PREFIX failed for {Prefix}", prefix);
        }

        return Task.CompletedTask;
    }
}
