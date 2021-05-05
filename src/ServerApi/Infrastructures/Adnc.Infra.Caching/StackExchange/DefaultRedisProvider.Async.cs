﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Adnc.Infra.Caching.Core;


namespace Adnc.Infra.Caching.StackExchange
{
    /// <summary>
    /// Default redis caching provider.
    /// </summary>
    public partial class DefaultRedisProvider : AbstractRedisDistributedCache, IRedisDistributedCache
    {
        /// <summary>
        /// Gets the specified cacheKey async.
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cacheKey">Cache key.</param>
        /// <param name="type">Object Type.</param>
        protected override async Task<object> BaseGetAsync(string cacheKey, Type type)
        {
            ArgumentCheck.NotNullOrWhiteSpace(cacheKey, nameof(cacheKey));

            var result = await _redisDb.StringGetAsync(cacheKey);
            if (!result.IsNull)
            {
                _cacheStats.OnHit();

                if (_cacheOptions.EnableLogging)
                    _logger?.LogInformation($"Cache Hit : cachekey = {cacheKey}");

                var value = _serializer.Deserialize(result, type);
                return value;
            }
            else
            {
                _cacheStats.OnMiss();

                if (_cacheOptions.EnableLogging)
                    _logger?.LogInformation($"Cache Missed : cachekey = {cacheKey}");

                return null;
            }
        }

        /// <summary>
        /// Gets the specified cacheKey, dataRetriever and expiration async.
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cacheKey">Cache key.</param>
        /// <param name="dataRetriever">Data retriever.</param>
        /// <param name="expiration">Expiration.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        protected override async Task<CacheValue<T>> BaseGetAsync<T>(string cacheKey, Func<Task<T>> dataRetriever, TimeSpan expiration)
        {
            ArgumentCheck.NotNullOrWhiteSpace(cacheKey, nameof(cacheKey));
            ArgumentCheck.NotNegativeOrZero(expiration, nameof(expiration));

            var result = await _redisDb.StringGetAsync(cacheKey);
            if (!result.IsNull)
            {
                _cacheStats.OnHit();

                if (_cacheOptions.EnableLogging)
                    _logger?.LogInformation($"Cache Hit : cachekey = {cacheKey}");

                var value = _serializer.Deserialize<T>(result);
                return new CacheValue<T>(value, true);
            }

            _cacheStats.OnMiss();

            if (_cacheOptions.EnableLogging)
                _logger?.LogInformation($"Cache Missed : cachekey = {cacheKey}");

            var flag = await _redisDb.StringSetAsync($"{cacheKey}_Lock", 1, TimeSpan.FromMilliseconds(_cacheOptions.LockMs), When.NotExists);

            if (!flag)
            {
                await Task.Delay(_cacheOptions.SleepMs);
                return await GetAsync(cacheKey, dataRetriever, expiration);
            }

            var item = await dataRetriever();
            if (item != null)
            {
                await SetAsync(cacheKey, item, expiration);

                //remove mutex key
                await _redisDb.KeyDeleteAsync($"{cacheKey}_Lock");
                return new CacheValue<T>(item, true);
            }
            else
            {
                //remove mutex key
                await _redisDb.KeyDeleteAsync($"{cacheKey}_Lock");
                return CacheValue<T>.NoValue;
            }
        }

        /// <summary>
        /// Gets the specified cacheKey async.
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cacheKey">Cache key.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        protected override async Task<CacheValue<T>> BaseGetAsync<T>(string cacheKey)
        {
            ArgumentCheck.NotNullOrWhiteSpace(cacheKey, nameof(cacheKey));

            var result = await _redisDb.StringGetAsync(cacheKey);
            if (!result.IsNull)
            {
                _cacheStats.OnHit();

                if (_cacheOptions.EnableLogging)
                    _logger?.LogInformation($"Cache Hit : cachekey = {cacheKey}");

                var value = _serializer.Deserialize<T>(result);
                return new CacheValue<T>(value, true);
            }
            else
            {
                _cacheStats.OnMiss();

                if (_cacheOptions.EnableLogging)
                    _logger?.LogInformation($"Cache Missed : cachekey = {cacheKey}");

                return CacheValue<T>.NoValue;
            }
        }

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <returns>The count.</returns>
        /// <param name="prefix">Prefix.</param>
        protected override Task<int> BaseGetCountAsync(string prefix = "")
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                var allCount = 0;

                foreach (var server in _servers)
                    allCount += (int)server.DatabaseSize(_redisDb.Database);

                return Task.FromResult(allCount);
            }

            return Task.FromResult(this.SearchRedisKeys(this.HandlePrefix(prefix)).Length);
        }

        /// <summary>
        /// Removes the specified cacheKey async.
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cacheKey">Cache key.</param>
        protected override async Task BaseRemoveAsync(string cacheKey)
        {
            ArgumentCheck.NotNullOrWhiteSpace(cacheKey, nameof(cacheKey));

            await _redisDb.KeyDeleteAsync(cacheKey);
        }

        /// <summary>
        /// Sets the specified cacheKey, cacheValue and expiration async.
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cacheKey">Cache key.</param>
        /// <param name="cacheValue">Cache value.</param>
        /// <param name="expiration">Expiration.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        protected override async Task BaseSetAsync<T>(string cacheKey, T cacheValue, TimeSpan expiration)
        {
            ArgumentCheck.NotNullOrWhiteSpace(cacheKey, nameof(cacheKey));
            ArgumentCheck.NotNull(cacheValue, nameof(cacheValue));
            ArgumentCheck.NotNegativeOrZero(expiration, nameof(expiration));

            if (_cacheOptions.MaxRdSecond > 0)
            {
                var addSec = new Random().Next(1, _cacheOptions.MaxRdSecond);
                expiration = expiration.Add(TimeSpan.FromSeconds(addSec));
            }

            await _redisDb.StringSetAsync(
                    cacheKey,
                    _serializer.Serialize(cacheValue),
                    expiration);
        }

        /// <summary>
        /// Existses the specified cacheKey async.
        /// </summary>
        /// <returns>The async.</returns>
        /// <param name="cacheKey">Cache key.</param>
        protected override async Task<bool> BaseExistsAsync(string cacheKey)
        {
            ArgumentCheck.NotNullOrWhiteSpace(cacheKey, nameof(cacheKey));

            return await _redisDb.KeyExistsAsync(cacheKey);
        }

        /// <summary>
        /// Removes cached item by cachekey's prefix async.
        /// </summary>
        /// <param name="prefix">Prefix of CacheKey.</param>
        protected override async Task BaseRemoveByPrefixAsync(string prefix)
        {
            ArgumentCheck.NotNullOrWhiteSpace(prefix, nameof(prefix));

            prefix = this.HandlePrefix(prefix);

            if (_cacheOptions.EnableLogging)
                _logger?.LogInformation($"RemoveByPrefixAsync : prefix = {prefix}");

            var redisKeys = this.SearchRedisKeys(prefix);

            await _redisDb.KeyDeleteAsync(redisKeys);
        }

        /// <summary>
        /// Sets all async.
        /// </summary>
        /// <returns>The all async.</returns>
        /// <param name="values">Values.</param>
        /// <param name="expiration">Expiration.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        protected override async Task BaseSetAllAsync<T>(IDictionary<string, T> values, TimeSpan expiration)
        {
            ArgumentCheck.NotNegativeOrZero(expiration, nameof(expiration));
            ArgumentCheck.NotNullAndCountGTZero(values, nameof(values));

            var tasks = new List<Task>();

            foreach (var item in values)
                tasks.Add(SetAsync(item.Key, item.Value, expiration));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Gets all async.
        /// </summary>
        /// <returns>The all async.</returns>
        /// <param name="cacheKeys">Cache keys.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        protected override async Task<IDictionary<string, CacheValue<T>>> BaseGetAllAsync<T>(IEnumerable<string> cacheKeys)
        {
            ArgumentCheck.NotNullAndCountGTZero(cacheKeys, nameof(cacheKeys));

            var keyArray = cacheKeys.ToArray();
            var values = await _redisDb.StringGetAsync(keyArray.Select(k => (RedisKey)k).ToArray());

            var result = new Dictionary<string, CacheValue<T>>();
            for (int i = 0; i < keyArray.Length; i++)
            {
                var cachedValue = values[i];
                if (!cachedValue.IsNull)
                    result.Add(keyArray[i], new CacheValue<T>(_serializer.Deserialize<T>(cachedValue), true));
                else
                    result.Add(keyArray[i], CacheValue<T>.NoValue);
            }

            return result;
        }

        /// <summary>
        /// Gets the by prefix async.
        /// </summary>
        /// <returns>The by prefix async.</returns>
        /// <param name="prefix">Prefix.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        protected override async Task<IDictionary<string, CacheValue<T>>> BaseGetByPrefixAsync<T>(string prefix)
        {
            ArgumentCheck.NotNullOrWhiteSpace(prefix, nameof(prefix));

            prefix = this.HandlePrefix(prefix);

            var redisKeys = this.SearchRedisKeys(prefix);

            var values = (await _redisDb.StringGetAsync(redisKeys)).ToArray();

            var result = new Dictionary<string, CacheValue<T>>();
            for (int i = 0; i < redisKeys.Length; i++)
            {
                var cachedValue = values[i];
                if (!cachedValue.IsNull)
                    result.Add(redisKeys[i], new CacheValue<T>(_serializer.Deserialize<T>(cachedValue), true));
                else
                    result.Add(redisKeys[i], CacheValue<T>.NoValue);
            }

            return result;
        }

        /// <summary>
        /// Removes all async.
        /// </summary>
        /// <returns>The all async.</returns>
        /// <param name="cacheKeys">Cache keys.</param>
        protected override async Task BaseRemoveAllAsync(IEnumerable<string> cacheKeys)
        {
            ArgumentCheck.NotNullAndCountGTZero(cacheKeys, nameof(cacheKeys));

            var redisKeys = cacheKeys.Where(k => !string.IsNullOrEmpty(k)).Select(k => (RedisKey)k).ToArray();
            if (redisKeys.Length > 0)
                await _redisDb.KeyDeleteAsync(redisKeys);
        }

        /// <summary>
        /// Flush All Cached Item async.
        /// </summary>
        /// <returns>The async.</returns>
        protected override async Task BaseFlushAsync()
        {
            if (_cacheOptions.EnableLogging)
                _logger?.LogInformation("Redis -- FlushAsync");

            var tasks = new List<Task>();

            foreach (var server in _servers)
            {
                tasks.Add(server.FlushDatabaseAsync(_redisDb.Database));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Tries the set async.
        /// </summary>
        /// <returns>The set async.</returns>
        /// <param name="cacheKey">Cache key.</param>
        /// <param name="cacheValue">Cache value.</param>
        /// <param name="expiration">Expiration.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        protected override Task<bool> BaseTrySetAsync<T>(string cacheKey, T cacheValue, TimeSpan expiration)
        {
            ArgumentCheck.NotNullOrWhiteSpace(cacheKey, nameof(cacheKey));
            ArgumentCheck.NotNull(cacheValue, nameof(cacheValue));
            ArgumentCheck.NotNegativeOrZero(expiration, nameof(expiration));

            if (_cacheOptions.MaxRdSecond > 0)
            {
                var addSec = new Random().Next(1, _cacheOptions.MaxRdSecond);
                expiration = expiration.Add(TimeSpan.FromSeconds(addSec));
            }

            return _redisDb.StringSetAsync(
                cacheKey,
                _serializer.Serialize(cacheValue),
                expiration,
                When.NotExists
                );
        }

        /// <summary>
        /// Get the expiration of cache key
        /// </summary>
        /// <param name="cacheKey">cache key</param>
        /// <returns>expiration</returns>
        protected override async Task<TimeSpan> BaseGetExpirationAsync(string cacheKey)
        {
            ArgumentCheck.NotNullOrWhiteSpace(cacheKey, nameof(cacheKey));

            var timeSpan = await _redisDb.KeyTimeToLiveAsync(cacheKey);
            return timeSpan.HasValue ? timeSpan.Value : TimeSpan.Zero;
        }
    }
}