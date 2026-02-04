using System.Text.Json;
using BuilderService;

namespace BuilderService.UnitTests.Utils;

public class DateTimeConverterTests
{
    private readonly JsonSerializerOptions _options;

    public DateTimeConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new DateTimeConverter());
    }

    [Fact]
    public void Write_FormatsAsUtcString()
    {
        var dt = new DateTime(2024, 1, 15, 8, 30, 0, DateTimeKind.Utc);
        var json = JsonSerializer.Serialize(dt, _options);
        json.Should().Be("\"2024-01-15 08:30:00\"");
    }

    [Fact]
    public void Write_ConvertsLocalTimeToUtc()
    {
        var dt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Local);
        var json = JsonSerializer.Serialize(dt, _options);
        var utc = dt.ToUniversalTime();
        var expected = $"\"{utc:yyyy-MM-dd HH:mm:ss}\"";
        json.Should().Be(expected);
    }

    [Fact]
    public void Read_ParsesValidDateString()
    {
        var json = "\"2024-01-15 08:30:00\"";
        var dt = JsonSerializer.Deserialize<DateTime>(json, _options);
        dt.Year.Should().Be(2024);
        dt.Month.Should().Be(1);
        dt.Day.Should().Be(15);
        dt.Hour.Should().Be(8);
        dt.Minute.Should().Be(30);
    }

    [Fact]
    public void Read_ParsesIso8601String()
    {
        var json = "\"2024-01-15T08:30:00Z\"";
        var dt = JsonSerializer.Deserialize<DateTime>(json, _options);
        dt.Year.Should().Be(2024);
    }
}
