namespace BuildService
{
    public class PowerShellOutputResult
    {
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public int OutputOffset { get; set; }
        public int ErrorOffset { get; set; }
        public PowerShellTaskStatus Status { get; set; }
        public int? ExitCode { get; set; }
    }
}
