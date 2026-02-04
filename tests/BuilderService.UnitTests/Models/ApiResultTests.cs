using BuilderService;

namespace BuilderService.UnitTests.Models;

public class ApiResultTests
{
    [Fact]
    public void GenericApiResult_DefaultCode_IsZero()
    {
        var result = new ApiResult<string>();
        result.Code.Should().Be(0);
    }

    [Fact]
    public void GenericApiResult_TimestampIsSetToRecentTicks()
    {
        var before = DateTime.UtcNow.Ticks;
        var result = new ApiResult<string>();
        var after = DateTime.UtcNow.Ticks;

        result.Timestamp.Should().BeGreaterThanOrEqualTo(before);
        result.Timestamp.Should().BeLessThanOrEqualTo(after);
    }

    [Fact]
    public void GenericApiResult_MessageDefaultsToNull()
    {
        var result = new ApiResult<string>();
        result.Message.Should().BeNull();
    }

    [Fact]
    public void NonGenericApiResult_InheritsFromGenericObject()
    {
        var result = new ApiResult();
        result.Should().BeAssignableTo<ApiResult<object>>();
    }
}
