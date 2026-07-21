using Xunit;
using POS.Domain.ValueObjects;
using FluentAssertions;

namespace POS.Tests.UnitTests;

public class ArabicNameTests
{
    [Fact]
    public void Create_ValidName_ShouldReturnArabicName()
    {
        var result = ArabicName.Create("منتج جديد");
        result.Should().NotBeNull();
        result.Value.Should().Be("منتج جديد");
    }

    [Fact]
    public void Create_EmptyName_ShouldThrow()
    {
        Action act = () => ArabicName.Create("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NullName_ShouldThrow()
    {
        Action act = () => ArabicName.Create(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TooLongName_ShouldThrow()
    {
        Action act = () => ArabicName.Create(new string('أ', 201));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ExactlyMaxLength_ShouldSucceed()
    {
        var result = ArabicName.Create(new string('أ', 200));
        result.Should().NotBeNull();
        result.Value.Should().HaveLength(200);
    }
}
