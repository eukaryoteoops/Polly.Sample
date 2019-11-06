using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using Polly;
using Polly.Caching;
using Polly.Caching.Distributed;
using Polly.Registry;
using Polly.Wrap;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PollySample.Cache
{
    public class CacheBase
    {
        private readonly IDistributedCache _cache;
        private TimeSpan DefaultTimeToLive => TimeSpan.FromMinutes(5);

        public CacheBase(IDistributedCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        ///     Wrap policy
        /// </summary>
        /// <param name="key"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        private AsyncPolicyWrap<string> GetPolicyWrap(string key, TimeSpan? duration)
        {
            PolicyRegistry registry = new PolicyRegistry
            {
                // https://github.com/App-vNext/Polly/wiki/Cache
                { key, Policy.CacheAsync<string>(
                        cacheProvider: _cache.AsAsyncCacheProvider<string>(),
                        ttlStrategy: new RelativeTtl(duration ?? DefaultTimeToLive),
                        //new ContextualTtl(), used with context[ContextualTtl.TimeSpanKey], define ttl in different calls
                        //new SlidingTtl(duration ?? DefaultTimeToLive), each time the cache item is touched, ttl extend.
                        //new ResultTtl(), used for vary ttl base on T model pass in
                        cacheKeyStrategy: (ctx) => ctx.OperationKey, //OperationKey is default cacheKey
                        onCacheGet: (ctx,cacheKey) => Console.WriteLine($"Get : {cacheKey}"),
                        onCacheMiss: (ctx,cacheKey) => Console.WriteLine($"Miss : {cacheKey}"),
                        onCachePut: (ctx,cacheKey) => Console.WriteLine($"Put : {cacheKey}"),
                        onCacheGetError: (ctx,str,ex) => throw ex,
                        onCachePutError: (ctx,str,ex) => throw ex)
                }
            };

            var asyncPolicy = registry.Get<IAsyncPolicy<string>>(key);
            return Policy
                .Handle<IOException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
                .WrapAsync(asyncPolicy);
        }

        public async ValueTask<string> Get(string key, Task<string> obj, TimeSpan? duration = null)
        {
            var result = await GetPolicyWrap(key, duration).ExecuteAsync(async ctx => await obj, new Context(key));
            //var result = await GetPolicyWrap(key).ExecuteAndCaptureAsync(async o => await obj, new Context(key));

            if (string.IsNullOrEmpty(result))
                return string.Empty;
            return result;
        }

        public async ValueTask<T> Get<T>(string key, Task<T> obj, TimeSpan? duration = null)
        {
            var result = await GetPolicyWrap(key, duration).ExecuteAsync(async ctx => JsonConvert.SerializeObject(await obj), new Context(key));

            if (string.IsNullOrEmpty(result))
                return default(T);
            return JsonConvert.DeserializeObject<T>(result);
        }

        public async ValueTask Set(string key, string obj, TimeSpan? duration = null)
        {
            await _cache.SetStringAsync(key, obj,
                new DistributedCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = duration ?? DefaultTimeToLive, SlidingExpiration = DefaultTimeToLive });
        }

        public async ValueTask Set<T>(string key, T obj, TimeSpan? duration = null)
        {
            await _cache.SetStringAsync(key, JsonConvert.SerializeObject(obj),
                new DistributedCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = duration ?? DefaultTimeToLive, SlidingExpiration = DefaultTimeToLive });
        }
    }
}
