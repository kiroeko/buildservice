namespace BuilderService
{
    public class Controller : Microsoft.AspNetCore.Mvc.Controller
    {
        protected ApiResult<T> ApiResult<T>(T data)
        {
            return new ApiResult<T>
            {
                Code = 200,
                Data = data,
                Message = "OK"
            };
        }
    }
}
