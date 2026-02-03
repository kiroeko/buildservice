using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace BuilderService
{
    public class PowerShellService : BackgroundService
    {
        private readonly int _maxTasks;
        private readonly TimeSpan _taskExpiry;
        private readonly TimeSpan _taskTimeout;
        private readonly ConcurrentDictionary<string, PowerShellTask> _tasks = new();
        private readonly Channel<PowerShellTask> _queue = Channel.CreateUnbounded<PowerShellTask>();

        public PowerShellService(IConfiguration configuration)
        {
            _maxTasks = configuration.GetValue("PowerShellService:MaxTasks", 100);
            _taskExpiry = TimeSpan.FromMinutes(configuration.GetValue("PowerShellService:TaskExpiryMinutes", 60));
            _taskTimeout = TimeSpan.FromMinutes(configuration.GetValue("PowerShellService:TaskTimeoutMinutes", 30));
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
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(task.ScriptPath) ?? ""
                };

                process.Start();

                using var cts = new CancellationTokenSource(_taskTimeout);

                var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    task.Error = $"Process timed out after {_taskTimeout.TotalMinutes} minutes and was killed";
                    task.Status = PowerShellTaskStatus.TimedOut;
                    return;
                }

                task.Output = await outputTask;
                task.Error = await errorTask;
                task.ExitCode = process.ExitCode;
                task.Status = process.ExitCode == 0 ? PowerShellTaskStatus.Completed : PowerShellTaskStatus.Failed;
            }
            catch (Exception ex)
            {
                task.Error = ex.Message;
                task.Status = PowerShellTaskStatus.Failed;
            }
            finally
            {
                task.CompletedAt = DateTime.UtcNow;
            }
        }

        private void Cleanup()
        {
            var now = DateTime.UtcNow;

            // Remove completed tasks older than 1 hour
            foreach (var task in _tasks.Values)
            {
                if (task.CompletedAt.HasValue && now - task.CompletedAt.Value > _taskExpiry)
                    _tasks.TryRemove(task.Id, out _);
            }

            // If still over limit, remove oldest completed tasks
            while (_tasks.Count > _maxTasks)
            {
                var oldest = _tasks.Values
                    .Where(t => t.CompletedAt.HasValue)
                    .OrderBy(t => t.CreatedAt)
                    .FirstOrDefault();

                if (oldest == null) break;
                _tasks.TryRemove(oldest.Id, out _);
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
