namespace BuilderService
{
    public class StatusService
    {
        public bool IsReady { get; set; }
    }

    public static class StatusServiceExtensions
    {
        public static IServiceCollection AddStatusService(this IServiceCollection self)
            => self.AddSingleton<StatusService>();
    }
}
