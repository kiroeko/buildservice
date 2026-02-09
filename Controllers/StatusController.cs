using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BuildService
{
    [Route("api/[controller]")]
    [ApiController]
    [DisableRateLimiting]
    public class StatusController : Controller
    {
        [HttpGet]
        public ApiResult<bool> Get([FromServices] StatusService ss)
        {
            return ApiResult(ss.IsReady);
        }
    }
}
