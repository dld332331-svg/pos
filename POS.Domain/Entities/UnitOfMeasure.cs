namespace POS.Domain.Entities;

/// <summary>
/// Represents a unit of measure (e.g., kg, g, L, mL, piece, dozen).
/// Supports conversion between related units via a base unit reference.
/// </summary>
public class UnitOfMeasure : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ArabicName { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? ArabicSymbol { get; set; }

    /// <summary>
    /// The category of this unit (e.g., "Weight", "Volume", "Count").
    /// Only units within the same category can be converted between each other.
    /// </summary>
    public string Category { get; set; } = "Count";

    /// <summary>
    /// If true, this is the base/reference unit for its category.
    /// All conversions are expressed relative to the base unit (base factor = 1).
    /// </summary>
    public bool IsBaseUnit { get; set; }

    /// <summary>
    /// Conversion factor relative to the base unit of the same category.
    /// E.g., for kg (base) factor=1, for g factor=0.001
    /// </summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>
    /// Precision for displaying quantities in this unit (number of decimal places).
    /// </summary>
    public int DecimalPlaces { get; set; } = 3;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
