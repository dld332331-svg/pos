using Xunit;
using POS.Infrastructure.Security;
using FluentAssertions;

namespace POS.Tests.UnitTests;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    // ========================================================================
    // HashPassword — Happy Path
    // ========================================================================

    [Fact]
    public void HashPassword_ShouldReturnNonEmptyString()
    {
        var hash = _hasher.HashPassword("test123");
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe("test123");
    }

    [Fact]
    public void HashPassword_SameInput_DifferentHashes()
    {
        var hash1 = _hasher.HashPassword("test123");
        var hash2 = _hasher.HashPassword("test123");
        hash1.Should().NotBe(hash2);
    }

    // ========================================================================
    // HashPassword — Guard Clause
    // ========================================================================

    [Fact]
    public void HashPassword_NullPassword_ShouldThrowArgumentException()
    {
        var act = () => _hasher.HashPassword(null!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void HashPassword_EmptyPassword_ShouldThrowArgumentException()
    {
        var act = () => _hasher.HashPassword("");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    // ========================================================================
    // VerifyPassword — Happy Path
    // ========================================================================

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

    // ========================================================================
    // VerifyPassword — Guard Clauses (null / empty)
    // ========================================================================

    [Fact]
    public void VerifyPassword_NullPassword_ShouldReturnFalse()
    {
        var hash = _hasher.HashPassword("test123");
        _hasher.VerifyPassword(null!, hash).Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_EmptyPassword_ShouldReturnFalse()
    {
        var hash = _hasher.HashPassword("test123");
        _hasher.VerifyPassword("", hash).Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_NullHash_ShouldReturnFalse()
    {
        _hasher.VerifyPassword("test123", null!).Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_EmptyHash_ShouldReturnFalse()
    {
        _hasher.VerifyPassword("test123", "").Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_NullPasswordAndNullHash_ShouldReturnFalse()
    {
        _hasher.VerifyPassword(null!, null!).Should().BeFalse();
    }

    // ========================================================================
    // VerifyPassword — Malformed Hash
    // ========================================================================

    [Fact]
    public void VerifyPassword_MalformedHash_WrongPartsCount_ShouldReturnFalse()
    {
        // Hash with only 2 parts (should have 3)
        var hash = _hasher.HashPassword("test123");
        var twoPartHash = hash.Split(':')[0] + ":" + hash.Split(':')[1];
        _hasher.VerifyPassword("test123", twoPartHash).Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_MalformedHash_SinglePart_ShouldReturnFalse()
    {
        _hasher.VerifyPassword("test123", "justasinglepart").Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_MalformedHash_EmptyParts_ShouldReturnFalse()
    {
        _hasher.VerifyPassword("test123", "::").Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_MalformedHash_InvalidIterations_ShouldReturnFalse()
    {
        // Non-numeric iterations value
        var hash = _hasher.HashPassword("test123");
        var parts = hash.Split(':');
        var badHash = $"NOT_A_NUMBER:{parts[1]}:{parts[2]}";
        _hasher.VerifyPassword("test123", badHash).Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_MalformedHash_NegativeIterations_ShouldThrowArgumentOutOfRangeException()
    {
        // int.TryParse("-1", ...) succeeds, but Rfc2898DeriveBytes.Pbkdf2
        // throws ArgumentOutOfRangeException for negative iterations
        var hash = _hasher.HashPassword("test123");
        var parts = hash.Split(':');
        var badHash = $"-1:{parts[1]}:{parts[2]}";
        var act = () => _hasher.VerifyPassword("test123", badHash);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void VerifyPassword_MalformedHash_InvalidBase64Salt_ShouldThrowFormatException()
    {
        // Convert.FromBase64String throws FormatException for invalid base64
        var act = () => _hasher.VerifyPassword("test123", "10000:NOT_VALID_BASE64!!:AAAA");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void VerifyPassword_MalformedHash_InvalidBase64Hash_ShouldThrowFormatException()
    {
        // Convert.FromBase64String throws FormatException for invalid base64
        var act = () => _hasher.VerifyPassword("test123", "10000:AAAA:NOT_VALID_BASE64!!");
        act.Should().Throw<FormatException>();
    }

    // ========================================================================
    // VerifyPassword — Edge Cases
    // ========================================================================

    [Fact]
    public void VerifyPassword_WhitespacePassword_ShouldReturnFalse()
    {
        var hash = _hasher.HashPassword("test123");
        _hasher.VerifyPassword("   ", hash).Should().BeFalse();
    }
}
