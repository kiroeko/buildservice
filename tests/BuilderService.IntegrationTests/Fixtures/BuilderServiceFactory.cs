using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BuilderService.IntegrationTests.Fixtures;

public class BuilderServiceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PowerShellService:TaskTimeoutMinutes"] = "1",
                ["PowerShellService:MaxTasks"] = "10",
                ["PowerShellService:CompletedTaskRetentionMinutes"] = "1",
                ["RateLimiting:PermitLimit"] = "1000",
                ["RateLimiting:WindowSeconds"] = "60",
            });
        });
    }
}

public class RateLimitedFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = "2",
                ["RateLimiting:WindowSeconds"] = "60",
            });
        });
    }
}
