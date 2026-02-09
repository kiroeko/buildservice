using BuildService;

namespace BuildService.UnitTests.Services;

public class PowerShellServiceSubmitTests
{
    private static IConfiguration BuildConfig(int maxTasks = 100, int retentionMinutes = 60, int timeoutMinutes = 30)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PowerShellService:MaxTasks"] = maxTasks.ToString(),
                ["PowerShellService:CompletedTaskRetentionMinutes"] = retentionMinutes.ToString(),
                ["PowerShellService:TaskTimeoutMinutes"] = timeoutMinutes.ToString(),
            })
            .Build();
    }

    private static PowerShellService CreateService(int maxTasks = 100)
        => new(BuildConfig(maxTasks: maxTasks));

    [Fact]
    public void Submit_ReturnsNonEmptyTaskId()
    {
        var service = CreateService();
        var id = service.Submit("test.ps1");
        id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Submit_TaskAppearsInGetTask()
    {
        var service = CreateService();
        var id = service.Submit("test.ps1");
        var task = service.GetTask(id);
        task.Should().NotBeNull();
        task!.Id.Should().Be(id);
    }

    [Fact]
    public void Submit_TaskStatusIsPending()
    {
        var service = CreateService();
        var id = service.Submit("test.ps1");
        var task = service.GetTask(id);
        task!.Status.Should().Be(PowerShellTaskStatus.Pending);
    }

    [Fact]
    public void GetTask_NonExistentId_ReturnsNull()
    {
        var service = CreateService();
        service.GetTask("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetAllTasks_ReturnsAllSubmittedTasks()
    {
        var service = CreateService();
        service.Submit("a.ps1");
        service.Submit("b.ps1");
        service.Submit("c.ps1");
        service.GetAllTasks().Should().HaveCount(3);
    }

    [Fact]
    public void GetAllTasks_OrderedByCreatedAtDescending()
    {
        var service = CreateService();
        service.Submit("a.ps1");
        service.Submit("b.ps1");
        var tasks = service.GetAllTasks();
        tasks[0].CreatedAt.Should().BeOnOrAfter(tasks[1].CreatedAt);
    }

    [Fact]
    public void IsFull_WhenBelowMax_ReturnsFalse()
    {
        var service = CreateService(maxTasks: 3);
        service.Submit("a.ps1");
        service.IsFull.Should().BeFalse();
    }

    [Fact]
    public void IsFull_WhenAtMax_ReturnsTrue()
    {
        var service = CreateService(maxTasks: 2);
        service.Submit("a.ps1");
        service.Submit("b.ps1");
        service.IsFull.Should().BeTrue();
    }

    [Fact]
    public void StopTask_NonExistentId_ReturnsFalse()
    {
        var service = CreateService();
        service.StopTask("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void StopTask_PendingTask_ReturnsTrue()
    {
        var service = CreateService();
        var id = service.Submit("test.ps1");
        service.StopTask(id).Should().BeTrue();
    }

    [Fact]
    public void StopTask_PendingTask_CancelsCts()
    {
        var service = CreateService();
        var id = service.Submit("test.ps1");
        service.StopTask(id);
        var task = service.GetTask(id);
        task!.Cts.IsCancellationRequested.Should().BeTrue();
    }

    [Theory]
    [InlineData(PowerShellTaskStatus.Completed)]
    [InlineData(PowerShellTaskStatus.Failed)]
    [InlineData(PowerShellTaskStatus.TimedOut)]
    [InlineData(PowerShellTaskStatus.Cancelled)]
    public void StopTask_TerminalStatus_ReturnsFalse(PowerShellTaskStatus status)
    {
        var service = CreateService();
        var id = service.Submit("test.ps1");
        var task = service.GetTask(id)!;
        task.Status = status;
        service.StopTask(id).Should().BeFalse();
    }

    [Fact]
    public void StopTask_RunningTask_ReturnsTrue()
    {
        var service = CreateService();
        var id = service.Submit("test.ps1");
        var task = service.GetTask(id)!;
        task.Status = PowerShellTaskStatus.Running;
        service.StopTask(id).Should().BeTrue();
        task.Cts.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void Constructor_NullOutputDirectory_UsesDefault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PowerShellService:MaxTasks"] = "100",
                ["PowerShellService:CompletedTaskRetentionMinutes"] = "60",
                ["PowerShellService:TaskTimeoutMinutes"] = "30",
                ["PowerShellService:OutputDirectory"] = null,
            })
            .Build();
        var service = new PowerShellService(config);
        service.Should().NotBeNull();
    }
}
