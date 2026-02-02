using Microsoft.AspNetCore.Mvc;

namespace BuilderService
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatusController : Controller
    {
        [HttpGet]
        public ApiResult<bool> Get([FromServices] StatusService ss)
        {
            return ApiResult(ss.IsReady);
        }
    }
}
