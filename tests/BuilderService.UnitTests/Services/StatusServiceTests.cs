using BuilderService;

namespace BuilderService.UnitTests.Services;

public class StatusServiceTests
{
    [Fact]
    public void IsReady_DefaultsToFalse()
    {
        var service = new StatusService();
        service.IsReady.Should().BeFalse();
    }

    [Fact]
    public void IsReady_SetToTrue_ReturnsTrue()
    {
        var service = new StatusService();
        service.IsReady = true;
        service.IsReady.Should().BeTrue();
    }
}
