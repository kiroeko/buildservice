using BuildService;

namespace BuildService.UnitTests.Controllers;

public class StatusControllerTests
{
    [Fact]
    public void Get_WhenReady_ReturnsCode200WithTrue()
    {
        var controller = new StatusController();
        var ss = new StatusService { IsReady = true };

        var result = controller.Get(ss);
        result.Code.Should().Be(200);
        result.Data.Should().BeTrue();
    }

    [Fact]
    public void Get_WhenNotReady_ReturnsCode200WithFalse()
    {
        var controller = new StatusController();
        var ss = new StatusService();

        var result = controller.Get(ss);
        result.Code.Should().Be(200);
        result.Data.Should().BeFalse();
    }
}
