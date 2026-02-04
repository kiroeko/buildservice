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
        var result = task.GetOutputSince();
        result.Output.Should().Be("hello" + Environment.NewLine);
    }

    [Fact]
    public void AppendOutput_MultipleLines_AppendsAll()
    {
        var task = new PowerShellTask();
        task.AppendOutput("line1");
        task.AppendOutput("line2");
        var result = task.GetOutputSince();
        result.Output.Should().Be("line1" + Environment.NewLine + "line2" + Environment.NewLine);
    }

    [Fact]
    public void AppendError_SingleLine_AppendsWithNewline()
    {
        var task = new PowerShellTask();
        task.AppendError("err");
        var result = task.GetOutputSince();
        result.Error.Should().Be("err" + Environment.NewLine);
    }

    [Fact]
    public void GetOutputSince_WithOffset_ReturnsIncremental()
    {
        var task = new PowerShellTask();
        task.AppendOutput("line1");
        task.AppendOutput("line2");

        var first = task.GetOutputSince();
        first.Output.Should().Contain("line1");
        first.OutputOffset.Should().BeGreaterThan(0);

        task.AppendOutput("line3");
        var second = task.GetOutputSince(first.OutputOffset);
        second.Output.Should().Contain("line3");
        second.Output.Should().NotContain("line1");
    }

    [Fact]
    public void GetOutputSince_OffsetExceedsLength_ReturnsEmpty()
    {
        var task = new PowerShellTask();
        task.AppendOutput("hello");
        var result = task.GetOutputSince(outputOffset: 99999);
        result.Output.Should().BeEmpty();
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

        var result = task.GetOutputSince();
        var lines = result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(count);
    }

    [Fact]
    public void GetOutputSince_NegativeOffset_ClampsToZero()
    {
        var task = new PowerShellTask();
        task.AppendOutput("hello");
        var result = task.GetOutputSince(outputOffset: -5, errorOffset: -10);
        result.Output.Should().Be("hello" + Environment.NewLine);
        result.OutputOffset.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetOutputSince_ReturnsStatusAndExitCode()
    {
        var task = new PowerShellTask();
        task.Status = PowerShellTaskStatus.Completed;
        task.ExitCode = 0;
        task.AppendOutput("done");

        var result = task.GetOutputSince();
        result.Status.Should().Be(PowerShellTaskStatus.Completed);
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public void GetOutputSince_ErrorOffset_ReturnsIncrementalError()
    {
        var task = new PowerShellTask();
        task.AppendError("err1");
        var first = task.GetOutputSince();
        first.Error.Should().Contain("err1");

        task.AppendError("err2");
        var second = task.GetOutputSince(errorOffset: first.ErrorOffset);
        second.Error.Should().Contain("err2");
        second.Error.Should().NotContain("err1");
    }

    [Fact]
    public void PersistOutput_WritesFilesAndReleasesMemory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ps-test-{Guid.NewGuid():N}");
        try
        {
            var task = new PowerShellTask();
            task.AppendOutput("stdout content");
            task.AppendError("stderr content");

            var success = task.PersistOutput(dir);

            success.Should().BeTrue();
            task.OutputPersisted.Should().BeTrue();
            task.OutputFilePath.Should().NotBeNull();
            task.ErrorFilePath.Should().NotBeNull();
            File.Exists(task.OutputFilePath).Should().BeTrue();
            File.Exists(task.ErrorFilePath).Should().BeTrue();
            File.ReadAllText(task.OutputFilePath!).Should().Contain("stdout content");
            File.ReadAllText(task.ErrorFilePath!).Should().Contain("stderr content");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PersistOutput_ThenGetOutputSince_ReadsFromFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ps-test-{Guid.NewGuid():N}");
        try
        {
            var task = new PowerShellTask();
            task.AppendOutput("line1");
            task.AppendOutput("line2");

            var before = task.GetOutputSince();
            task.PersistOutput(dir);

            var after = task.GetOutputSince();
            after.Output.Should().Contain("line1");
            after.Output.Should().Contain("line2");
            after.OutputOffset.Should().Be(before.OutputOffset);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PersistOutput_ThenGetOutputSince_WithOffset_ReturnsIncremental()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ps-test-{Guid.NewGuid():N}");
        try
        {
            var task = new PowerShellTask();
            task.AppendOutput("line1");
            task.AppendOutput("line2");

            var first = task.GetOutputSince();
            var midOffset = first.OutputOffset / 2;

            task.PersistOutput(dir);

            var result = task.GetOutputSince(outputOffset: midOffset);
            result.Output.Should().NotBeEmpty();
            result.OutputOffset.Should().Be(first.OutputOffset);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PersistOutput_ThenGetOutputSince_OffsetExceedsFile_ReturnsEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ps-test-{Guid.NewGuid():N}");
        try
        {
            var task = new PowerShellTask();
            task.AppendOutput("short");
            task.PersistOutput(dir);

            var result = task.GetOutputSince(outputOffset: 99999);
            result.Output.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AppendOutput_AfterPersist_IsIgnored()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ps-test-{Guid.NewGuid():N}");
        try
        {
            var task = new PowerShellTask();
            task.AppendOutput("before");
            task.PersistOutput(dir);

            task.AppendOutput("after");

            var result = task.GetOutputSince();
            result.Output.Should().Contain("before");
            result.Output.Should().NotContain("after");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void DeleteOutputFiles_RemovesFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ps-test-{Guid.NewGuid():N}");
        try
        {
            var task = new PowerShellTask();
            task.AppendOutput("data");
            task.PersistOutput(dir);

            var stdout = task.OutputFilePath!;
            var stderr = task.ErrorFilePath!;
            File.Exists(stdout).Should().BeTrue();
            File.Exists(stderr).Should().BeTrue();

            task.DeleteOutputFiles();

            File.Exists(stdout).Should().BeFalse();
            File.Exists(stderr).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void DeleteOutputFiles_NoFiles_DoesNotThrow()
    {
        var task = new PowerShellTask();
        var act = () => task.DeleteOutputFiles();
        act.Should().NotThrow();
    }

    [Fact]
    public void PersistOutput_InvalidPath_ReturnsFalse()
    {
        var task = new PowerShellTask();
        task.AppendOutput("data");

        var success = task.PersistOutput("Z:\\nonexistent\\deeply\\nested\\invalid");

        success.Should().BeFalse();
        task.OutputPersisted.Should().BeFalse();
        task.OutputFilePath.Should().BeNull();
        task.ErrorFilePath.Should().BeNull();

        // StringBuilder should still be intact
        var result = task.GetOutputSince();
        result.Output.Should().Contain("data");
    }

    [Fact]
    public void GetOutputSince_AfterPersist_FileDeleted_ReturnsEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ps-test-{Guid.NewGuid():N}");
        try
        {
            var task = new PowerShellTask();
            task.AppendOutput("content");
            task.AppendError("err");
            task.PersistOutput(dir);

            // Manually delete files to simulate external deletion
            File.Delete(task.OutputFilePath!);
            File.Delete(task.ErrorFilePath!);

            var result = task.GetOutputSince();
            result.Output.Should().BeEmpty();
            result.Error.Should().BeEmpty();
            result.OutputOffset.Should().Be(0);
            result.ErrorOffset.Should().Be(0);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AppendError_AfterPersist_IsIgnored()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ps-test-{Guid.NewGuid():N}");
        try
        {
            var task = new PowerShellTask();
            task.AppendError("before");
            task.PersistOutput(dir);

            task.AppendError("after");

            var result = task.GetOutputSince();
            result.Error.Should().Contain("before");
            result.Error.Should().NotContain("after");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetOutputSince_FromFile_NegativeOffset_ClampsToZero()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ps-test-{Guid.NewGuid():N}");
        try
        {
            var task = new PowerShellTask();
            task.AppendOutput("hello");
            task.AppendError("world");
            task.PersistOutput(dir);

            var result = task.GetOutputSince(outputOffset: -5, errorOffset: -10);
            result.Output.Should().Contain("hello");
            result.Error.Should().Contain("world");
            result.OutputOffset.Should().BeGreaterThan(0);
            result.ErrorOffset.Should().BeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
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
