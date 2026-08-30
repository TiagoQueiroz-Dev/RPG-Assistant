using System.Text.Json;
using Microsoft.Extensions.Logging;
using RpgWorld.Application.Caching;
using StackExchange.Redis;

namespace RpgWorld.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService, IAsyncDisposable
{
    private readonly RedisOptions _options;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly Lazy<Task<ConnectionMultiplexer>> _connection;

    public RedisCacheService(
        RedisOptions options,
        ILogger<RedisCacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (!options.Enabled)
        {
            throw new ArgumentException(
                "RedisCacheService requires Redis to be enabled.",
                nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException(
                "A Redis connection string is required when Redis is enabled.",
                nameof(options));
        }

        _options = options;
        _logger = logger;
        _connection = new Lazy<Task<ConnectionMultiplexer>>(ConnectAsync);
    }

    public async Task<CacheReadResult<T>> GetAsync<T>(
        CacheKey key,
        CancellationToken cancellationToken = default)
        where T : notnull
    {
        try
        {
            var database = await GetDatabaseAsync(cancellationToken);
            var value = await database.StringGetAsync(Namespaced(key))
                .WaitAsync(cancellationToken);

            if (value.IsNull)
            {
                return CacheReadResult<T>.Miss();
            }

            var bytes = (byte[]?)value;
            var deserialized = bytes is null
                ? default
                : JsonSerializer.Deserialize<T>(bytes, _serializerOptions);

            return deserialized is null
                ? CacheReadResult<T>.Miss()
                : CacheReadResult<T>.Hit(deserialized);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogFallback(exception, key, "read");
            return CacheReadResult<T>.Miss();
        }
    }

    public async Task SetAsync<T>(
        CacheKey key,
        T value,
        CacheEntryOptions options,
        CancellationToken cancellationToken = default)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var database = await GetDatabaseAsync(cancellationToken);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _serializerOptions);

            await database.StringSetAsync(
                    Namespaced(key),
                    bytes,
                    options.AbsoluteExpiration)
                .WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogFallback(exception, key, "write");
        }
    }

    public async Task RemoveAsync(
        CacheKey key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = await GetDatabaseAsync(cancellationToken);
            await database.KeyDeleteAsync(Namespaced(key))
                .WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogFallback(exception, key, "remove");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_connection.IsValueCreated)
        {
            return;
        }

        try
        {
            var connection = await _connection.Value;
            connection.Dispose();
        }
        catch (RedisException)
        {
            // A failed optional cache connection has no resource left to release.
        }
    }

    private Task<ConnectionMultiplexer> ConnectAsync()
    {
        var configuration = ConfigurationOptions.Parse(_options.ConnectionString!);
        configuration.AbortOnConnectFail = false;
        configuration.ConnectRetry = 2;
        configuration.ConnectTimeout = 1_000;
        configuration.AsyncTimeout = 1_000;
        configuration.ClientName = _options.InstanceName;

        return ConnectionMultiplexer.ConnectAsync(configuration);
    }

    private async Task<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        var connection = await _connection.Value.WaitAsync(cancellationToken);
        return connection.GetDatabase();
    }

    private RedisKey Namespaced(CacheKey key) =>
        $"{_options.InstanceName}:{key.Value}";

    private static bool IsRedisFailure(Exception exception) =>
        exception is RedisException or TimeoutException;

    private void LogFallback(Exception exception, CacheKey key, string operation)
    {
        _logger.LogWarning(
            exception,
            "Redis {Operation} failed for {CacheKey}; using the durable source fallback.",
            operation,
            key.Value);
    }
}

