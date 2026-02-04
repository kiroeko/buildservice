using BuilderService;

namespace BuilderService.UnitTests.Models;

public class PowerShellTaskTests
{
    [Fact]
    public void Constructor_SetsIdToNonEmpty()
    {
        var task = new PowerShellTask();
        task.Id.Should().NotBeNullOrEmpty();
        task.Id.Should().HaveLength(32); // Guid.ToString("N")
    }

    [Fact]
    public void Constructor_SetsStatusToPending()
    {
        var task = new PowerShellTask();
        task.Status.Should().Be(PowerShellTaskStatus.Pending);
    }

    [Fact]
    public void Constructor_SetsCreatedAtToApproximatelyNow()
    {
        var before = DateTime.UtcNow;
        var task = new PowerShellTask();
        var after = DateTime.UtcNow;

        task.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Constructor_SetsCompletedAtToNull()
    {
        var task = new PowerShellTask();
        task.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_SetsExitCodeToNull()
    {
        var task = new PowerShellTask();
        task.ExitCode.Should().BeNull();
    }

    [Fact]
    public void AppendOutput_SingleLine_AppendsWithNewline()
    {
        var task = new PowerShellTask();
        task.AppendOutput("hello");
        task.Output.Should().Be("hello" + Environment.NewLine);
    }

    [Fact]
    public void AppendOutput_MultipleLines_AppendsAll()
    {
        var task = new PowerShellTask();
        task.AppendOutput("line1");
        task.AppendOutput("line2");
        task.Output.Should().Be("line1" + Environment.NewLine + "line2" + Environment.NewLine);
    }

    [Fact]
    public void AppendError_SingleLine_AppendsWithNewline()
    {
        var task = new PowerShellTask();
        task.AppendError("err");
        task.Error.Should().Be("err" + Environment.NewLine);
    }

    [Fact]
    public void Output_SetterOverwritesPreviousValue()
    {
        var task = new PowerShellTask();
        task.AppendOutput("old");
        task.Output = "new";
        task.Output.Should().Be("new");
    }

    [Fact]
    public void Error_SetterOverwritesPreviousValue()
    {
        var task = new PowerShellTask();
        task.AppendError("old");
        task.Error = "new";
        task.Error.Should().Be("new");
    }

    [Fact]
    public void AppendOutput_ConcurrentCalls_DoesNotCorruptData()
    {
        var task = new PowerShellTask();
        var count = 100;

        Parallel.For(0, count, i =>
        {
            task.AppendOutput($"line{i}");
        });

        var lines = task.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(count);
    }

    [Fact]
    public void Cts_InitiallyNotCancelled()
    {
        var task = new PowerShellTask();
        task.Cts.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Cts_AfterCancel_IsCancellationRequestedIsTrue()
    {
        var task = new PowerShellTask();
        task.Cts.Cancel();
        task.Cts.IsCancellationRequested.Should().BeTrue();
    }
}
