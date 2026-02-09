using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace BuildService
{
    public class PowerShellService : BackgroundService
    {
        private readonly int _maxTasks;
        private readonly TimeSpan _taskExpiry;
        private readonly TimeSpan _taskTimeout;
        private readonly string _outputDirectory;
        private readonly ConcurrentDictionary<string, PowerShellTask> _tasks = new();
        private readonly Channel<PowerShellTask> _queue = Channel.CreateUnbounded<PowerShellTask>();

        public PowerShellService(IConfiguration configuration)
        {
            _maxTasks = configuration.GetValue("PowerShellService:MaxTasks", 100);
            _taskExpiry = TimeSpan.FromMinutes(configuration.GetValue("PowerShellService:CompletedTaskRetentionMinutes", 60));
            _taskTimeout = TimeSpan.FromMinutes(configuration.GetValue("PowerShellService:TaskTimeoutMinutes", 30));
            _outputDirectory = configuration.GetValue("PowerShellService:OutputDirectory", "task-outputs") ?? "task-outputs";
        }

        public bool IsFull => _tasks.Count >= _maxTasks;

        public string Submit(string scriptPath)
        {
            var task = new PowerShellTask { ScriptPath = scriptPath };
            _tasks[task.Id] = task;
            _queue.Writer.TryWrite(task);
            return task.Id;
        }

        public PowerShellTask? GetTask(string id)
        {
            _tasks.TryGetValue(id, out var task);
            return task;
        }

        public bool StopTask(string id)
        {
            if (!_tasks.TryGetValue(id, out var task))
                return false;

            if (task.Status != PowerShellTaskStatus.Running && task.Status != PowerShellTaskStatus.Pending)
                return false;

            task.Cts.Cancel();
            return true;
        }

        public List<PowerShellTask> GetAllTasks()
        {
            return _tasks.Values.OrderByDescending(t => t.CreatedAt).ToList();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var task in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await RunTaskAsync(task);
                Cleanup();
            }
        }

        private async Task RunTaskAsync(PowerShellTask task)
        {
            task.Status = PowerShellTaskStatus.Running;

            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{task.ScriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(task.ScriptPath) ?? ""
                };

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        task.AppendOutput(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        task.AppendError(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var timeoutCts = new CancellationTokenSource(_taskTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(task.Cts.Token, timeoutCts.Token);

                try
                {
                    await process.WaitForExitAsync(linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    if (task.Cts.IsCancellationRequested)
                    {
                        task.AppendError("Task was cancelled by user");
                        task.Status = PowerShellTaskStatus.Cancelled;
                    }
                    else
                    {
                        task.AppendError($"Process timed out after {_taskTimeout.TotalMinutes} minutes and was killed");
                        task.Status = PowerShellTaskStatus.TimedOut;
                    }
                    return;
                }
                task.ExitCode = process.ExitCode;
                task.Status = process.ExitCode == 0 ? PowerShellTaskStatus.Completed : PowerShellTaskStatus.Failed;
            }
            catch (Exception ex)
            {
                task.AppendError(ex.Message);
                task.Status = PowerShellTaskStatus.Failed;
            }
            finally
            {
                task.CompletedAt = DateTime.UtcNow;
                task.PersistOutput(_outputDirectory);
            }
        }

        private void Cleanup()
        {
            var now = DateTime.UtcNow;

            // Remove completed tasks older than retention period
            foreach (var task in _tasks.Values)
            {
                if (task.CompletedAt.HasValue && now - task.CompletedAt.Value > _taskExpiry)
                {
                    if (_tasks.TryRemove(task.Id, out _))
                        task.DeleteOutputFiles();
                }
            }

            // If still over limit, remove oldest completed tasks
            while (_tasks.Count > _maxTasks)
            {
                var oldest = _tasks.Values
                    .Where(t => t.CompletedAt.HasValue)
                    .OrderBy(t => t.CreatedAt)
                    .FirstOrDefault();

                if (oldest == null) break;
                if (_tasks.TryRemove(oldest.Id, out _))
                    oldest.DeleteOutputFiles();
            }
        }
    }

    public static class PowerShellServiceExtensions
    {
        public static IServiceCollection AddPowerShellService(this IServiceCollection self)
        {
            self.AddSingleton<PowerShellService>();
            self.AddHostedService(sp => sp.GetRequiredService<PowerShellService>());
            return self;
        }
    }
}
