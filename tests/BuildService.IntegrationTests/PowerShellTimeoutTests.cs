using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildService;
using BuildService.IntegrationTests.Fixtures;

namespace BuildService.IntegrationTests;

public class PowerShellTimeoutTests : IClassFixture<ShortTimeoutFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(), new DateTimeConverter() }
    };

    public PowerShellTimeoutTests(ShortTimeoutFactory factory)
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

    [Fact]
    public async Task PostRun_SlowScript_EventuallyTimedOut()
    {
        var taskId = await SubmitScript(GetFixturePath("test-slow.ps1"));
        var task = await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));
        task.Status.Should().Be(PowerShellTaskStatus.TimedOut);
    }

    [Fact]
    public async Task Cleanup_ExpiredTask_RemovedFromTaskList()
    {
        var taskId = await SubmitScript(GetFixturePath("test-success.ps1"));
        var task = await WaitForTerminalStatus(taskId, TimeSpan.FromSeconds(30));
        task.Status.Should().Be(PowerShellTaskStatus.Completed);

        // Wait for expiry (retention is ~0.6 seconds)
        await Task.Delay(2000);

        // Submit another task to trigger Cleanup()
        var taskId2 = await SubmitScript(GetFixturePath("test-success.ps1"));
        await WaitForTerminalStatus(taskId2, TimeSpan.FromSeconds(30));

        // Original task should be cleaned up
        var response = await _client.GetFromJsonAsync<ApiResult<PowerShellTask>>(
            $"/api/powershell/{taskId}", JsonOptions);
        response!.Code.Should().Be(404);
    }
}
