using System.Text;
using System.Text.Json.Serialization;

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
        private StringBuilder? _output = new();
        private StringBuilder? _error = new();

        [JsonIgnore]
        public bool OutputPersisted { get; private set; }

        [JsonIgnore]
        public string? OutputFilePath { get; private set; }

        [JsonIgnore]
        public string? ErrorFilePath { get; private set; }

        public void AppendOutput(string line)
        {
            lock (_lock) _output?.AppendLine(line);
        }

        public void AppendError(string line)
        {
            lock (_lock) _error?.AppendLine(line);
        }

        public PowerShellOutputResult GetOutputSince(int outputOffset = 0, int errorOffset = 0)
        {
            lock (_lock)
            {
                if (OutputPersisted)
                    return GetOutputFromFile(outputOffset, errorOffset);

                var outLen = _output?.Length ?? 0;
                var errLen = _error?.Length ?? 0;

                if (outputOffset < 0) outputOffset = 0;
                if (errorOffset < 0) errorOffset = 0;
                if (outputOffset > outLen) outputOffset = outLen;
                if (errorOffset > errLen) errorOffset = errLen;

                var outputStr = outputOffset < outLen
                    ? _output!.ToString(outputOffset, outLen - outputOffset)
                    : string.Empty;
                var errorStr = errorOffset < errLen
                    ? _error!.ToString(errorOffset, errLen - errorOffset)
                    : string.Empty;

                return new PowerShellOutputResult
                {
                    Output = outputStr,
                    Error = errorStr,
                    OutputOffset = outLen,
                    ErrorOffset = errLen,
                    Status = Status,
                    ExitCode = ExitCode
                };
            }
        }

        private PowerShellOutputResult GetOutputFromFile(int outputOffset, int errorOffset)
        {
            var output = ReadFileFromOffset(OutputFilePath, ref outputOffset);
            var error = ReadFileFromOffset(ErrorFilePath, ref errorOffset);

            return new PowerShellOutputResult
            {
                Output = output,
                Error = error,
                OutputOffset = outputOffset,
                ErrorOffset = errorOffset,
                Status = Status,
                ExitCode = ExitCode
            };
        }

        private static string ReadFileFromOffset(string? filePath, ref int offset)
        {
            if (filePath == null || !File.Exists(filePath))
            {
                offset = 0;
                return string.Empty;
            }

            if (offset < 0) offset = 0;

            var content = File.ReadAllText(filePath, Encoding.UTF8);
            var totalLen = content.Length;

            if (offset >= totalLen)
            {
                offset = totalLen;
                return string.Empty;
            }

            var result = content[offset..];
            offset = totalLen;
            return result;
        }

        public bool PersistOutput(string directory)
        {
            lock (_lock)
            {
                var stdoutPath = Path.Combine(directory, $"{Id}.stdout.log");
                var stderrPath = Path.Combine(directory, $"{Id}.stderr.log");
                try
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(stdoutPath, _output?.ToString() ?? string.Empty, Encoding.UTF8);
                    File.WriteAllText(stderrPath, _error?.ToString() ?? string.Empty, Encoding.UTF8);

                    OutputFilePath = stdoutPath;
                    ErrorFilePath = stderrPath;
                    _output = null;
                    _error = null;
                    OutputPersisted = true;
                    return true;
                }
                catch
                {
                    try { File.Delete(stdoutPath); } catch { }
                    try { File.Delete(stderrPath); } catch { }
                    OutputFilePath = null;
                    ErrorFilePath = null;
                    return false;
                }
            }
        }

        public void DeleteOutputFiles()
        {
            if (OutputFilePath != null) try { File.Delete(OutputFilePath); } catch { }
            if (ErrorFilePath != null) try { File.Delete(ErrorFilePath); } catch { }
        }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        [JsonIgnore]
        public CancellationTokenSource Cts { get; } = new();
    }
}
