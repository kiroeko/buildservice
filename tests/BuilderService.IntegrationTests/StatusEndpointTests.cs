using System.Net;
using System.Net.Http.Json;
using BuilderService.IntegrationTests.Fixtures;

namespace BuilderService.IntegrationTests;

public class StatusEndpointTests : IClassFixture<BuilderServiceFactory>
{
    private readonly HttpClient _client;

    public StatusEndpointTests(BuilderServiceFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetStatus_Returns200()
    {
        var response = await _client.GetAsync("/api/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStatus_ReturnsIsReadyTrue()
    {
        var response = await _client.GetFromJsonAsync<ApiResult<bool>>("/api/status");
        response.Should().NotBeNull();
        response!.Code.Should().Be(200);
        response.Data.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_HasCorrectApiResultStructure()
    {
        var response = await _client.GetFromJsonAsync<ApiResult<bool>>("/api/status");
        response.Should().NotBeNull();
        response!.Timestamp.Should().BeGreaterThan(0);
        response.Message.Should().Be("OK");
    }
}
