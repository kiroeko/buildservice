using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildService;
using BuildService.IntegrationTests.Fixtures;

namespace BuildService.IntegrationTests;

public class PowerShellEndpointTests : IClassFixture<BuildServiceFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new DateTimeConverter() }
    };

    public PowerShellEndpointTests(BuildServiceFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string GetFixturePath(string scriptName)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", scriptName);

    private async Task<string> SubmitScript(string scriptPath)
    {
        var body = new { scriptPath };
        var response = await _client.PostAsJsonAsync("/api/powershell/run", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<string>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Code.Should().Be(200);
        return result.Data;
    }

    private async Task<PowerShellTask> WaitForTerminalStatus(string taskId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var response = await _client.GetFromJsonAsync<ApiResult<PowerShellTask>>(
                $"/api/powershell/{taskId}", JsonOptions);

            if (response?.Data?.Status is PowerShellTaskStatus.Completed
                or PowerShellTaskStatus.Failed
                or PowerShellTaskStatus.Cancelled
                or PowerShellTaskStatus.TimedOut)
            {
                return response.Data;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Task {taskId} did not reach terminal status within {timeout}");
    }

    // Validation tests

    [Fact]
    public async Task PostRun_EmptyScriptPath_ReturnsError()
    {
        var body = new { scriptPath = "" };
        var response = await _client.PostAsJsonAsync("/api/powershell/run", body);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<string>>(JsonOptions);
        result!.Code.Should().Be(400);
    }

    [Fact]
    public async Task PostRun_NonPs1File_ReturnsError()
    {
        var body = new { scriptPath = "test.txt" };
        var response = await _client.PostAsJsonAsync("/api/powershell/run", body);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<string>>(JsonOptions);
        result!.Code.Should().Be(400);
    }

    [Fact]
    public async Task PostRun_ForbiddenCharacters_ReturnsError()
    {
        var body = new { scriptPath = "test;evil.ps1" };
        var response = await _client.PostAsJsonAsync("/api/powershell/run", body);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<string>>(JsonOptions);
        result!.Code.Should().Be(400);
    }

    [Fact]
    public async Task PostRun_FileNotFound_ReturnsError()
    {
        var body = new { scriptPath = @"C:\nonexistent\test.ps1" };
        var response = await _client.PostAsJsonAsync("/api/powershell/run", body);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<string>>(JsonOptions);
        result!.Code.Should().Be(404);
    }

    // Lifecycle tests

    [Fact]
    public async Task PostRun_ValidScript_ReturnsTaskId()
    {
        var taskId = await SubmitScript(GetFixturePath("test-success.ps1"));
        taskId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostRun_SuccessScript_EventuallyCompleted()
    {
        var taskId = await SubmitScript(GetFixturePath("test-success.ps1"));
        var task = await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));
        task.Status.Should().Be(PowerShellTaskStatus.Completed);
        task.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task PostRun_SuccessScript_CapturesOutput()
    {
        var taskId = await SubmitScript(GetFixturePath("test-output.ps1"));
        var task = await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));
        task.Status.Should().Be(PowerShellTaskStatus.Completed);

        var outputResponse = await _client.GetFromJsonAsync<ApiResult<PowerShellOutputResult>>(
            $"/api/powershell/{taskId}/output", JsonOptions);
        outputResponse.Should().NotBeNull();
        outputResponse!.Data.Output.Should().Contain("line1");
        outputResponse.Data.Output.Should().Contain("line2");
    }

    [Fact]
    public async Task PostRun_FailScript_EventuallyFailed()
    {
        var taskId = await SubmitScript(GetFixturePath("test-fail.ps1"));
        var task = await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));
        task.Status.Should().Be(PowerShellTaskStatus.Failed);
        task.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task PostRun_ThenStop_EventuallyCancelled()
    {
        var taskId = await SubmitScript(GetFixturePath("test-slow.ps1"));

        // Wait a bit for task to start
        await Task.Delay(1000);

        var stopResponse = await _client.PostAsync($"/api/powershell/{taskId}/stop", null);
        stopResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var task = await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));
        task.Status.Should().Be(PowerShellTaskStatus.Cancelled);
    }

    [Fact]
    public async Task GetAllTasks_ReturnsSubmittedTasks()
    {
        var taskId = await SubmitScript(GetFixturePath("test-success.ps1"));
        var response = await _client.GetFromJsonAsync<ApiResult<List<PowerShellTask>>>(
            "/api/powershell", JsonOptions);
        response.Should().NotBeNull();
        response!.Data.Should().Contain(t => t.Id == taskId);
    }

    // Output endpoint tests

    [Fact]
    public async Task GetOutput_NonExistentTask_Returns404()
    {
        var response = await _client.GetAsync("/api/powershell/nonexistent/output");
        var result = await response.Content.ReadFromJsonAsync<ApiResult<PowerShellOutputResult>>(JsonOptions);
        result!.Code.Should().Be(404);
    }

    [Fact]
    public async Task GetOutput_CompletedTask_ReturnsOutputFromFile()
    {
        var taskId = await SubmitScript(GetFixturePath("test-output.ps1"));
        await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));

        var result = await _client.GetFromJsonAsync<ApiResult<PowerShellOutputResult>>(
            $"/api/powershell/{taskId}/output", JsonOptions);

        result.Should().NotBeNull();
        result!.Data.Output.Should().Contain("line1");
        result.Data.Output.Should().Contain("line2");
        result.Data.Status.Should().Be(PowerShellTaskStatus.Completed);
        result.Data.ExitCode.Should().Be(0);
        result.Data.OutputOffset.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetOutput_WithFullOffset_ReturnsEmpty()
    {
        var taskId = await SubmitScript(GetFixturePath("test-output.ps1"));
        await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));

        var first = await _client.GetFromJsonAsync<ApiResult<PowerShellOutputResult>>(
            $"/api/powershell/{taskId}/output", JsonOptions);

        var second = await _client.GetFromJsonAsync<ApiResult<PowerShellOutputResult>>(
            $"/api/powershell/{taskId}/output?outputOffset={first!.Data.OutputOffset}&errorOffset={first.Data.ErrorOffset}", JsonOptions);

        second!.Data.Output.Should().BeEmpty();
        second.Data.Error.Should().BeEmpty();
        second.Data.OutputOffset.Should().Be(first.Data.OutputOffset);
    }

    [Fact]
    public async Task GetOutput_WithExcessiveOffset_ReturnsEmpty()
    {
        var taskId = await SubmitScript(GetFixturePath("test-output.ps1"));
        await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));

        var result = await _client.GetFromJsonAsync<ApiResult<PowerShellOutputResult>>(
            $"/api/powershell/{taskId}/output?outputOffset=999999", JsonOptions);

        result!.Data.Output.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTask_CompletedTask_DoesNotContainOutputFields()
    {
        var taskId = await SubmitScript(GetFixturePath("test-output.ps1"));
        await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));

        var response = await _client.GetAsync($"/api/powershell/{taskId}");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().NotContain("\"output\"");
        json.Should().NotContain("\"error\"");
        json.Should().NotContain("\"outputFilePath\"");
        json.Should().NotContain("\"errorFilePath\"");
        json.Should().NotContain("\"outputPersisted\"");
    }

    // JSON format tests

    [Fact]
    public async Task GetTask_ResponseUsesCamelCase()
    {
        var taskId = await SubmitScript(GetFixturePath("test-success.ps1"));
        var response = await _client.GetAsync($"/api/powershell/{taskId}");
        var json = await response.Content.ReadAsStringAsync();

        // camelCase properties
        json.Should().Contain("\"scriptPath\"");
        json.Should().Contain("\"status\"");
        json.Should().Contain("\"createdAt\"");

        // Should NOT have PascalCase
        json.Should().NotContain("\"ScriptPath\"");
        json.Should().NotContain("\"Status\"");
    }

    [Fact]
    public async Task GetTask_StatusIsEnumString()
    {
        var taskId = await SubmitScript(GetFixturePath("test-success.ps1"));
        await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));

        var response = await _client.GetAsync($"/api/powershell/{taskId}");
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"Completed\"");
    }
}
