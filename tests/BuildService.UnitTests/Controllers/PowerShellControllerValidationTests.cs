using BuildService;

namespace BuildService.UnitTests.Controllers;

public class PowerShellControllerValidationTests
{
    private readonly PowerShellController _controller;
    private readonly PowerShellService _service;

    public PowerShellControllerValidationTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PowerShellService:MaxTasks"] = "2",
                ["PowerShellService:CompletedTaskRetentionMinutes"] = "60",
                ["PowerShellService:TaskTimeoutMinutes"] = "30",
            })
            .Build();
        _service = new PowerShellService(config);
        _controller = new PowerShellController();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Run_EmptyOrNullScriptPath_Returns400(string? path)
    {
        var result = _controller.Run(new PowerShellRunRequest { ScriptPath = path! }, _service);
        result.Code.Should().Be(400);
        result.Message.Should().Contain("required");
    }

    [Theory]
    [InlineData("test.txt")]
    [InlineData("script.bat")]
    [InlineData("run.exe")]
    public void Run_NonPs1Extension_Returns400(string path)
    {
        var result = _controller.Run(new PowerShellRunRequest { ScriptPath = path }, _service);
        result.Code.Should().Be(400);
        result.Message.Should().Contain(".ps1");
    }

    [Theory]
    [InlineData("test\".ps1")]
    [InlineData("test`.ps1")]
    [InlineData("test$.ps1")]
    [InlineData("test|.ps1")]
    [InlineData("test;.ps1")]
    [InlineData("test&.ps1")]
    [InlineData("test<.ps1")]
    [InlineData("test>.ps1")]
    [InlineData("test(.ps1")]
    [InlineData("test).ps1")]
    [InlineData("test{.ps1")]
    [InlineData("test}.ps1")]
    public void Run_ForbiddenCharacters_Returns400(string path)
    {
        var result = _controller.Run(new PowerShellRunRequest { ScriptPath = path }, _service);
        result.Code.Should().Be(400);
        result.Message.Should().Contain("forbidden");
    }

    [Fact]
    public void Run_FileNotFound_Returns404()
    {
        var result = _controller.Run(
            new PowerShellRunRequest { ScriptPath = @"C:\nonexistent\test.ps1" },
            _service);
        result.Code.Should().Be(404);
    }

    [Fact]
    public void Run_QueueFull_Returns429()
    {
        // MaxTasks is 2, fill the queue
        _service.Submit("a.ps1");
        _service.Submit("b.ps1");

        // Use a path that passes validation but will hit IsFull check
        // We need a file that actually exists for File.Exists to pass
        // Since this is hard in unit tests, we verify IsFull returns true
        _service.IsFull.Should().BeTrue();
    }

    [Fact]
    public void Stop_NonExistentTask_Returns404()
    {
        var result = _controller.Stop("nonexistent", _service);
        result.Code.Should().Be(404);
    }

    [Fact]
    public void Get_NonExistentTask_Returns404()
    {
        var result = _controller.Get("nonexistent", _service);
        result.Code.Should().Be(404);
    }

    [Fact]
    public void Get_ExistingTask_Returns200()
    {
        var id = _service.Submit("test.ps1");
        var result = _controller.Get(id, _service);
        result.Code.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(id);
    }

    [Fact]
    public void GetAll_ReturnsAllTasks()
    {
        _service.Submit("a.ps1");
        _service.Submit("b.ps1");
        var result = _controller.GetAll(_service);
        result.Code.Should().Be(200);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public void Stop_CompletedTask_Returns400()
    {
        var id = _service.Submit("test.ps1");
        var task = _service.GetTask(id)!;
        task.Status = PowerShellTaskStatus.Completed;
        var result = _controller.Stop(id, _service);
        result.Code.Should().Be(400);
        result.Message.Should().Contain("not running");
    }

    [Fact]
    public void GetOutput_NonExistentTask_Returns404()
    {
        var result = _controller.GetOutput("nonexistent", service: _service);
        result.Code.Should().Be(404);
    }

    [Fact]
    public void GetOutput_ExistingTask_Returns200()
    {
        var id = _service.Submit("test.ps1");
        var task = _service.GetTask(id)!;
        task.AppendOutput("hello");
        var result = _controller.GetOutput(id, service: _service);
        result.Code.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data!.Output.Should().Contain("hello");
    }

    [Fact]
    public void Run_QueueFull_Returns429_ViaController()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ps-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var scriptPath = Path.Combine(tempDir, "dummy.ps1");
        File.WriteAllText(scriptPath, "exit 0");
        try
        {
            _service.Submit("a.ps1");
            _service.Submit("b.ps1");

            var result = _controller.Run(
                new PowerShellRunRequest { ScriptPath = scriptPath },
                _service);
            result.Code.Should().Be(429);
            result.Message.Should().Contain("full");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
