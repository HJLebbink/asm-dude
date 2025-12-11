using AsmDude3.Server.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AsmDude3.Server.Tests;

public class FoldingRangeProviderTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly FoldingRangeProvider _provider;

    public FoldingRangeProviderTests()
    {
        _loggerMock = new Mock<ILogger>();
        _provider = new FoldingRangeProvider(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var act = () => new FoldingRangeProvider(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void ProvideFoldingRanges_EmptyDocument_ReturnsEmpty()
    {
        var lines = Array.Empty<string>();
        var ranges = _provider.ProvideFoldingRanges(lines);
        ranges.Should().BeEmpty();
    }

    [Fact]
    public void ProvideFoldingRanges_RegionTags_CreatesFoldingRange()
    {
        var lines = new[] { "#region Test", "mov rax, rbx", "#endregion" };
        var ranges = _provider.ProvideFoldingRanges(lines);
        ranges.Should().Contain(r => r.StartLine == 0 && r.EndLine == 2 && r.Kind == "region");
    }
}
