using Xunit;
using POS.Infrastructure.Security;
using FluentAssertions;

namespace POS.Tests.UnitTests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ShouldReturnNonEmptyString()
    {
        var hash = _hasher.HashPassword("test123");
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe("test123");
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ShouldReturnTrue()
    {
        var hash = _hasher.HashPassword("test123");
        _hasher.VerifyPassword("test123", hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_IncorrectPassword_ShouldReturnFalse()
    {
        var hash = _hasher.HashPassword("test123");
        _hasher.VerifyPassword("wrong", hash).Should().BeFalse();
    }

    [Fact]
    public void HashPassword_SameInput_DifferentHashes()
    {
        var hash1 = _hasher.HashPassword("test123");
        var hash2 = _hasher.HashPassword("test123");
        hash1.Should().NotBe(hash2);
    }
}
