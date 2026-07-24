using Xunit;
using POS.Domain.ValueObjects;
using FluentAssertions;

namespace POS.Tests.UnitTests;

/// <summary>
/// Comprehensive unit tests for ArabicName value object.
/// Covers creation, validation, equality, hashing, operators, and edge cases
/// to close branch coverage gaps identified in the coverage report.
/// </summary>
public sealed class ArabicNameTests
{
    // ========================================================================
    // Creation — Valid Paths
    // ========================================================================

    [Fact]
    public void Create_ValidName_ShouldReturnArabicName()
    {
        var result = ArabicName.Create("منتج جديد");
        result.Should().NotBeNull();
        result.Value.Should().Be("منتج جديد");
    }

    [Fact]
    public void Create_ValidEnglishName_ShouldAccept()
    {
        var result = ArabicName.Create("Product Name");
        result.Value.Should().Be("Product Name");
    }

    [Fact]
    public void Create_MixedArabicAndEnglish_ShouldAccept()
    {
        var name = "منتج Product 123";
        var result = ArabicName.Create(name);
        result.Value.Should().Be(name);
    }

    [Fact]
    public void Create_SingleCharacter_ShouldSucceed()
    {
        var result = ArabicName.Create("أ");
        result.Value.Should().Be("أ");
    }

    [Fact]
    public void Create_SingleEnglishCharacter_ShouldSucceed()
    {
        var result = ArabicName.Create("A");
        result.Value.Should().Be("A");
    }

    [Fact]
    public void Create_WithSpecialCharacters_ShouldAccept()
    {
        var result = ArabicName.Create("٪٤٥٦");
        result.Value.Should().Be("٪٤٥٦");
    }

    [Fact]
    public void Create_ExactlyMaxLength_ShouldSucceed()
    {
        var result = ArabicName.Create(new string('أ', 200));
        result.Should().NotBeNull();
        result.Value.Should().HaveLength(200);
    }

    [Fact]
    public void Create_NumbersOnly_ShouldAccept()
    {
        var result = ArabicName.Create("١٢٣٤٥");
        result.Value.Should().Be("١٢٣٤٥");
    }

    // ========================================================================
    // Creation — Validation / Error Paths
    // ========================================================================

    [Fact]
    public void Create_NullName_ShouldThrowArgumentException()
    {
        var act = () => ArabicName.Create(null!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void Create_EmptyString_ShouldThrowArgumentException()
    {
        var act = () => ArabicName.Create("");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void Create_WhitespaceOnly_ShouldThrowArgumentException()
    {
        var act = () => ArabicName.Create("   ");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void Create_TabCharacter_ShouldThrowArgumentException()
    {
        var act = () => ArabicName.Create("\t\t");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void Create_NewlineOnly_ShouldThrowArgumentException()
    {
        var act = () => ArabicName.Create("\n\r");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void Create_TooLongName_ShouldThrowArgumentException()
    {
        var act = () => ArabicName.Create(new string('أ', 201));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot exceed 200*");
    }

    [Fact]
    public void Create_ExceedsMaxLengthByFar_ShouldThrow()
    {
        var act = () => ArabicName.Create(new string('أ', 1000));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot exceed 200*");
    }

    // ========================================================================
    // Trim Behavior
    // ========================================================================

    [Fact]
    public void Create_LeadingWhitespace_ShouldTrim()
    {
        var result = ArabicName.Create("   منتج");
        result.Value.Should().Be("منتج");
    }

    [Fact]
    public void Create_TrailingWhitespace_ShouldTrim()
    {
        var result = ArabicName.Create("منتج   ");
        result.Value.Should().Be("منتج");
    }

    [Fact]
    public void Create_BothSidesWhitespace_ShouldTrim()
    {
        var result = ArabicName.Create("  منتج  ");
        result.Value.Should().Be("منتج");
    }

    [Fact]
    public void Create_InternalSpaces_ShouldPreserve()
    {
        var result = ArabicName.Create("منتج جديد جداً");
        result.Value.Should().Be("منتج جديد جداً");
    }

    // ========================================================================
    // Equals — ArabicName? overload
    // ========================================================================

    [Fact]
    public void Equals_SameValue_ShouldReturnTrue()
    {
        var name1 = ArabicName.Create("منتج");
        var name2 = ArabicName.Create("منتج");

        name1.Equals(name2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ShouldReturnFalse()
    {
        var name1 = ArabicName.Create("منتج");
        var name2 = ArabicName.Create("خدمة");

        name1.Equals(name2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ShouldReturnFalse()
    {
        var name = ArabicName.Create("منتج");

        name.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_SameReference_ShouldReturnTrue()
    {
        var name = ArabicName.Create("منتج");

        name.Equals(name).Should().BeTrue();
    }

    [Fact]
    public void Equals_CaseSensitive_ShouldTreatDifferently()
    {
        var name1 = ArabicName.Create("Product");
        var name2 = ArabicName.Create("product");

        // ArabicName uses Ordinal comparison — case-sensitive
        name1.Equals(name2).Should().BeFalse();
    }

    [Fact]
    public void Equals_EnglishSameCase_ShouldReturnTrue()
    {
        var name1 = ArabicName.Create("Coffee");
        var name2 = ArabicName.Create("Coffee");

        name1.Equals(name2).Should().BeTrue();
    }

    // ========================================================================
    // Equals — object? overload
    // ========================================================================

    [Fact]
    public void ObjectEquals_SameValue_ShouldReturnTrue()
    {
        object name1 = ArabicName.Create("منتج");
        object name2 = ArabicName.Create("منتج");

        name1.Equals(name2).Should().BeTrue();
    }

    [Fact]
    public void ObjectEquals_DifferentType_ShouldReturnFalse()
    {
        var name = ArabicName.Create("منتج");
        var notName = "منتج";

        name.Equals(notName).Should().BeFalse();
    }

    [Fact]
    public void ObjectEquals_DifferentValue_ShouldReturnFalse()
    {
        object name1 = ArabicName.Create("منتج");
        object name2 = ArabicName.Create("خدمة");

        name1.Equals(name2).Should().BeFalse();
    }

    [Fact]
    public void ObjectEquals_NullObject_ShouldReturnFalse()
    {
        object? nullObj = null;
        var name = ArabicName.Create("منتج");

        name.Equals(nullObj).Should().BeFalse();
    }

    [Fact]
    public void ObjectEquals_SameReference_ShouldReturnTrue()
    {
        object name = ArabicName.Create("منتج");

        name.Equals(name).Should().BeTrue();
    }

    // ========================================================================
    // GetHashCode
    // ========================================================================

    [Fact]
    public void GetHashCode_SameValue_ShouldBeEqual()
    {
        var hash1 = ArabicName.Create("منتج").GetHashCode();
        var hash2 = ArabicName.Create("منتج").GetHashCode();

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_DifferentValues_ShouldDiffer()
    {
        var hash1 = ArabicName.Create("منتج").GetHashCode();
        var hash2 = ArabicName.Create("خدمة").GetHashCode();

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void GetHashCode_Consistent_ShouldReturnSameOnMultipleCalls()
    {
        var name = ArabicName.Create("منتج جديد");

        var hash1 = name.GetHashCode();
        var hash2 = name.GetHashCode();
        var hash3 = name.GetHashCode();

        hash1.Should().Be(hash2);
        hash2.Should().Be(hash3);
    }

    // ========================================================================
    // ToString
    // ========================================================================

    [Fact]
    public void ToString_ShouldReturnValue()
    {
        var name = ArabicName.Create("منتج");
        name.ToString().Should().Be("منتج");
    }

    [Fact]
    public void ToString_EnglishName_ShouldReturnEnglish()
    {
        var name = ArabicName.Create("Product");
        name.ToString().Should().Be("Product");
    }

    // ========================================================================
    // Implicit Operator (string)
    // ========================================================================

    [Fact]
    public void ImplicitOperator_NonNullArabicName_ShouldReturnValue()
    {
        var name = ArabicName.Create("منتج");
        string result = name;
        result.Should().Be("منتج");
    }

    [Fact]
    public void ImplicitOperator_NullArabicName_ShouldReturnEmptyString()
    {
        ArabicName? name = null;
        string result = name;
        result.Should().BeEmpty();
    }

    [Fact]
    public void ImplicitOperator_CanBeUsedInStringInterpolation()
    {
        var name = ArabicName.Create("منتج");
        var message = $"اسم: {name}";
        message.Should().Be("اسم: منتج");
    }

    [Fact]
    public void ImplicitOperator_CanBeUsedInStringConcat()
    {
        var name = ArabicName.Create("منتج");
        var message = "اسم: " + name;
        message.Should().Be("اسم: منتج");
    }

    // ========================================================================
    // Value Property
    // ========================================================================

    [Fact]
    public void Value_AfterTrim_ShouldNotHaveLeadingSpaces()
    {
        var name = ArabicName.Create("   منتج");
        name.Value.Should().NotStartWith(" ");
    }

    [Fact]
    public void Value_AfterTrim_ShouldNotHaveTrailingSpaces()
    {
        var name = ArabicName.Create("منتج   ");
        name.Value.Should().NotEndWith(" ");
    }

    // ========================================================================
    // Equality Consistency (Equals + GetHashCode contract)
    // ========================================================================

    [Fact]
    public void EqualsAndGetHashCode_Contract_EqualObjectsHaveEqualHashes()
    {
        var name1 = ArabicName.Create("منتج");
        var name2 = ArabicName.Create("منتج");

        name1.Equals(name2).Should().BeTrue();
        name1.GetHashCode().Should().Be(name2.GetHashCode());
    }

    [Fact]
    public void EqualsAndGetHashCode_Contract_DifferentObjectsShouldDiffer()
    {
        var name1 = ArabicName.Create("منتج");
        var name2 = ArabicName.Create("خدمة");

        name1.Equals(name2).Should().BeFalse();
        name1.GetHashCode().Should().NotBe(name2.GetHashCode());
    }

    // ========================================================================
    // Edge Cases
    // ========================================================================

    [Fact]
    public void Create_WithDiacritics_ShouldAccept()
    {
        var result = ArabicName.Create("مَنْتِج");
        result.Value.Should().Be("مَنْتِج");
    }

    [Fact]
    public void Create_WithZeroWidthCharacters_ShouldAccept()
    {
        var result = ArabicName.Create("\u200Bمنتج"); // ZWNJ + name
        result.Value.Should().Be("\u200Bمنتج");
    }
}
