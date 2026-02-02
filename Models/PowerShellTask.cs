namespace BuilderService
{
    public enum PowerShellTaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    public class PowerShellTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string ScriptPath { get; set; } = string.Empty;

        public PowerShellTaskStatus Status { get; set; } = PowerShellTaskStatus.Pending;

        public int? ExitCode { get; set; }

        public string Output { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }
    }
}
