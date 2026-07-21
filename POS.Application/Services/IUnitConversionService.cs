using POS.Application.DTOs;

namespace POS.Application.Services;

/// <summary>
/// Provides cross-unit conversion between UnitOfMeasure entities within the same category.
/// Conversions are expressed relative to each unit's ConversionFactor (relative to the base unit).
/// 
/// Formula: quantityInToUnit = quantityInFromUnit * (fromUnit.ConversionFactor / toUnit.ConversionFactor)
/// </summary>
public interface IUnitConversionService
{
    /// <summary>
    /// Converts a quantity from one unit to another within the same category.
    /// Both units are identified by their IDs.
    /// </summary>
    /// <param name="quantity">The quantity to convert.</param>
    /// <param name="fromUnitId">The source unit's ID.</param>
    /// <param name="toUnitId">The target unit's ID.</param>
    /// <returns>The converted quantity, rounded to JOD precision (3 decimal places).</returns>
    /// <exception cref="InvalidOperationException">If either unit is not found or categories differ.</exception>
    Task<decimal> ConvertAsync(decimal quantity, Guid fromUnitId, Guid toUnitId);

    /// <summary>
    /// Converts a quantity from a given unit to the base unit of its category.
    /// </summary>
    Task<decimal> ConvertToBaseAsync(decimal quantity, Guid unitId);

    /// <summary>
    /// Converts a quantity from the base unit of a category to a given unit.
    /// </summary>
    Task<decimal> ConvertFromBaseAsync(decimal quantity, Guid unitId);

    /// <summary>
    /// Loads all active units of measure.
    /// </summary>
    Task<List<UnitOfMeasureDto>> GetAllUnitsAsync();

    /// <summary>
    /// Gets units grouped by category for UI dropdown population.
    /// </summary>
    Task<Dictionary<string, List<UnitOfMeasureDto>>> GetUnitsByCategoryAsync();
}
