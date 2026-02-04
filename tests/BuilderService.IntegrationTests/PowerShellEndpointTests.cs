using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuilderService;
using BuilderService.IntegrationTests.Fixtures;

namespace BuilderService.IntegrationTests;

public class PowerShellEndpointTests : IClassFixture<BuilderServiceFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new DateTimeConverter() }
    };

    public PowerShellEndpointTests(BuilderServiceFactory factory)
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
        task.Output.Should().Contain("line1");
        task.Output.Should().Contain("line2");
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
