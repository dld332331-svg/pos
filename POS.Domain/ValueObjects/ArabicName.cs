namespace POS.Domain.ValueObjects;

/// <summary>
/// Value object representing an Arabic-language name with validation.
/// Ensures the name is not null/empty and does not exceed 200 characters.
/// </summary>
public sealed class ArabicName : IEquatable<ArabicName>
{
    private const int MaxLength = 200;

    /// <summary>
    /// The Arabic name value.
    /// </summary>
    public string Value { get; }

    private ArabicName(string value)
    {
        if (value is null)
            throw new ArgumentException("Arabic name cannot be null or empty.", nameof(value));
        Value = value.Trim();
        Validate();
    }

    /// <summary>
    /// Validates the Arabic name.
    /// Throws <see cref="ArgumentException"/> if the name is null, empty, or exceeds 200 characters.
    /// </summary>
    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            throw new ArgumentException("Arabic name cannot be null or empty.", nameof(Value));
        }

        if (Value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Arabic name cannot exceed {MaxLength} characters. Provided length: {Value.Length}.",
                nameof(Value));
        }
    }

    /// <summary>
    /// Creates a new <see cref="ArabicName"/> instance after validating the input.
    /// </summary>
    /// <param name="value">The Arabic name string.</param>
    /// <returns>A validated ArabicName instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty, or too long.</exception>
    public static ArabicName Create(string value) => new(value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ArabicName other && Equals(other);

    /// <inheritdoc/>
    public bool Equals(ArabicName? other)
    {
        if (other is null) return false;
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    /// <summary>
    /// Returns the Arabic name as a string.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts an <see cref="ArabicName"/> to its string value.
    /// </summary>
    public static implicit operator string(ArabicName? name) => name?.Value ?? string.Empty;
}