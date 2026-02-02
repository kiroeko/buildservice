using Microsoft.AspNetCore.Mvc;

namespace BuilderService
{
    [Route("api/[controller]")]
    [ApiController]
    public class PowerShellController : Controller
    {
        [HttpPost("run")]
        public ApiResult<string> Run([FromBody] PowerShellRunRequest request, [FromServices] PowerShellService service)
        {
            if (string.IsNullOrWhiteSpace(request.ScriptPath))
                return new ApiResult<string> { Code = 400, Data = string.Empty, Message = "scriptPath is required" };

            if (!request.ScriptPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                return new ApiResult<string> { Code = 400, Data = string.Empty, Message = "Only .ps1 files are allowed" };

            // Block characters that could be used for command injection
            char[] forbiddenChars = ['"', '\'', '`', '$', '&', '|', ';', '<', '>', '(', ')', '{', '}', '\n', '\r'];
            if (request.ScriptPath.IndexOfAny(forbiddenChars) >= 0)
                return new ApiResult<string> { Code = 400, Data = string.Empty, Message = "ScriptPath contains forbidden characters: \" ' ` $ & | ; < > ( ) { }" };

            if (!System.IO.File.Exists(request.ScriptPath))
                return new ApiResult<string> { Code = 404, Data = string.Empty, Message = "Script file not found" };

            if (service.IsFull)
                return new ApiResult<string> { Code = 429, Data = string.Empty, Message = "Task queue is full, please try again later" };

            var taskId = service.Submit(request.ScriptPath);
            return ApiResult(taskId);
        }

        [HttpGet("{id}")]
        public ApiResult<PowerShellTask?> Get(string id, [FromServices] PowerShellService service)
        {
            var task = service.GetTask(id);
            if (task == null)
                return new ApiResult<PowerShellTask?> { Code = 404, Data = null, Message = "Task not found" };

            return ApiResult<PowerShellTask?>(task);
        }

        [HttpGet]
        public ApiResult<List<PowerShellTask>> GetAll([FromServices] PowerShellService service)
        {
            return ApiResult(service.GetAllTasks());
        }
    }

    public class PowerShellRunRequest
    {
        public string ScriptPath { get; set; } = string.Empty;
    }
}
