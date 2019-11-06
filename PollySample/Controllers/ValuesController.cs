using Microsoft.AspNetCore.Mvc;
using PollySample.Cache;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PollySample.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly CacheBase _cache;

        public ValuesController(CacheBase cache)
        {
            _cache = cache;
        }

        [HttpGet("get1")]
        public async Task<IEnumerable<string>> Get()
        {
            var result = await _cache.Get("key", Task.FromResult("obj1"));
            return new string[] { result };
        }


        [HttpGet("get2")]
        public async Task<IEnumerable<string>> Get2()
        {
            await _cache.Set("key", "test");
            var result = await _cache.Get("key", Task.FromResult("obj2"));
            return new string[] { result };
        }
    }
}
