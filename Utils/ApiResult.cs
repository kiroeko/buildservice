namespace BuilderService
{
    public class ApiResult<T>
    {
        public int Code { get; set; }

        public T Data { get; set; } = default!;

        public long Timestamp { get; set; } = DateTime.UtcNow.Ticks;

        public string? Message { get; set; }
    }

    public class ApiResult : ApiResult<object>
    {}
}
