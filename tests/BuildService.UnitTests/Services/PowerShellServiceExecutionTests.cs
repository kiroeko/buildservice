using BuildService;

namespace BuildService.UnitTests.Services;

public class PowerShellServiceExecutionTests : IDisposable
{
    private readonly string _tempDir;

    public PowerShellServiceExecutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ps_exec_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static PowerShellService CreateService(int maxTasks = 100, int retentionMinutes = 60, int timeoutMinutes = 30)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PowerShellService:MaxTasks"] = maxTasks.ToString(),
                ["PowerShellService:CompletedTaskRetentionMinutes"] = retentionMinutes.ToString(),
                ["PowerShellService:TaskTimeoutMinutes"] = timeoutMinutes.ToString(),
            })
            .Build();
        return new PowerShellService(config);
    }

    private static async Task WaitForTaskCompletion(PowerShellService service, string taskId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var task = service.GetTask(taskId);
            if (task?.CompletedAt != null)
                return;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Task {taskId} did not complete within {timeout}");
    }

    [Fact]
    public async Task RunTask_InvalidWorkingDirectory_TaskStatusIsFailed()
    {
        // Using a path with a non-existent directory causes Process.Start to throw,
        // covering the catch(Exception) branch without any mocking.
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        try
        {
            var id = service.Submit(@"C:\nonexistent_dir_12345\test.ps1");
            await WaitForTaskCompletion(service, id, TimeSpan.FromSeconds(5));

            var task = service.GetTask(id)!;
            task.Status.Should().Be(PowerShellTaskStatus.Failed);
            task.CompletedAt.Should().NotBeNull();
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }
    }

    [Fact]
    public async Task RunTask_ProcessSucceeds_TaskStatusIsCompleted()
    {
        var scriptPath = Path.Combine(_tempDir, "success.ps1");
        File.WriteAllText(scriptPath, "exit 0");

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        try
        {
            var id = service.Submit(scriptPath);
            await WaitForTaskCompletion(service, id, TimeSpan.FromSeconds(10));

            var task = service.GetTask(id)!;
            task.Status.Should().Be(PowerShellTaskStatus.Completed);
            task.ExitCode.Should().Be(0);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }
    }

    [Fact]
    public async Task RunTask_ProcessExitsNonZero_TaskStatusIsFailed()
    {
        var scriptPath = Path.Combine(_tempDir, "fail.ps1");
        File.WriteAllText(scriptPath, "exit 1");

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        try
        {
            var id = service.Submit(scriptPath);
            await WaitForTaskCompletion(service, id, TimeSpan.FromSeconds(10));

            var task = service.GetTask(id)!;
            task.Status.Should().Be(PowerShellTaskStatus.Failed);
            task.ExitCode.Should().Be(1);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }
    }

    [Fact]
    public async Task Cleanup_OverLimit_RemovesOldestCompletedTask()
    {
        // Submit 3 tasks with invalid paths to a service with maxTasks=2.
        // All fail quickly (Win32Exception), then cleanup evicts the oldest.
        var service = CreateService(maxTasks: 2);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var id1 = service.Submit(@"C:\nonexistent_a_12345\a.ps1");
            var id2 = service.Submit(@"C:\nonexistent_b_12345\b.ps1");
            var id3 = service.Submit(@"C:\nonexistent_c_12345\c.ps1");

            await WaitForTaskCompletion(service, id3, TimeSpan.FromSeconds(5));

            // First task should have been evicted by cleanup (oldest completed)
            service.GetTask(id1).Should().BeNull();
            // Later tasks should still exist
            service.GetTask(id2).Should().NotBeNull();
            service.GetTask(id3).Should().NotBeNull();
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }
    }
}
