using System.Net;
using BuildService.IntegrationTests.Fixtures;

namespace BuildService.IntegrationTests;

public class RateLimitingTests : IClassFixture<RateLimitedFactory>
{
    private readonly HttpClient _client;

    public RateLimitingTests(RateLimitedFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ExceedingRateLimit_Returns429()
    {
        // PermitLimit is 2, send 3 requests
        await _client.GetAsync("/api/powershell");
        await _client.GetAsync("/api/powershell");
        var response = await _client.GetAsync("/api/powershell");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task StatusEndpoint_IsExemptFromRateLimit()
    {
        // Status has [DisableRateLimiting], should always succeed
        for (int i = 0; i < 5; i++)
        {
            var response = await _client.GetAsync("/api/status");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
