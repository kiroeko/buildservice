using System.Text;

namespace BuilderService
{
    public enum PowerShellTaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        TimedOut,
        Cancelled
    }

    public class PowerShellTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string ScriptPath { get; set; } = string.Empty;

        public PowerShellTaskStatus Status { get; set; } = PowerShellTaskStatus.Pending;

        public int? ExitCode { get; set; }

        private readonly object _lock = new();
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        public string Output { get { lock (_lock) return _output.ToString(); } set { lock (_lock) { _output.Clear(); _output.Append(value); } } }

        public string Error { get { lock (_lock) return _error.ToString(); } set { lock (_lock) { _error.Clear(); _error.Append(value); } } }

        public void AppendOutput(string line) { lock (_lock) _output.AppendLine(line); }
        public void AppendError(string line) { lock (_lock) _error.AppendLine(line); }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public CancellationTokenSource Cts { get; } = new();
    }
}
