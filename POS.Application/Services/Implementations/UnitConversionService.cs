using POS.Application.DTOs;
using POS.Domain.BusinessRules;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

/// <summary>
/// Implementation of <see cref="IUnitConversionService"/> that loads UnitOfMeasure entities
/// from the database via IUnitOfWork and performs conversion math.
/// </summary>
public class UnitConversionService : IUnitConversionService
{
    private readonly IUnitOfWork _unitOfWork;
    private List<UnitOfMeasureDto>? _cachedUnits;

    public UnitConversionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<decimal> ConvertAsync(decimal quantity, Guid fromUnitId, Guid toUnitId)
    {
        if (fromUnitId == toUnitId)
        {
            return MoneyPolicy.RoundToJOD(quantity);
        }

        var units = await GetAllUnitsAsync();
        var fromUnit = units.FirstOrDefault(u => u.Id == fromUnitId);
        var toUnit = units.FirstOrDefault(u => u.Id == toUnitId);

        if (fromUnit is null)
        {
            throw new InvalidOperationException($"وحدة القياس المصدر غير موجودة (ID: {fromUnitId})");
        }
        if (toUnit is null)
        {
            throw new InvalidOperationException($"وحدة القياس الهدف غير موجودة (ID: {toUnitId})");
        }

        if (!string.Equals(fromUnit.Category, toUnit.Category, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"لا يمكن التحويل بين '{fromUnit.Name}' ({fromUnit.Category}) و '{toUnit.Name}' ({toUnit.Category}) - فئات مختلفة");
        }

        if (toUnit.ConversionFactor == 0m)
        {
            throw new InvalidOperationException($"معامل التحويل لوحدة '{toUnit.Name}' يساوي صفراً");
        }

        // Convert: quantityInToUnit = quantityInFromUnit * (fromFactor / toFactor)
        var result = quantity * fromUnit.ConversionFactor / toUnit.ConversionFactor;
        return MoneyPolicy.RoundToJOD(result);
    }

    public async Task<decimal> ConvertToBaseAsync(decimal quantity, Guid unitId)
    {
        var units = await GetAllUnitsAsync();
        var unit = units.FirstOrDefault(u => u.Id == unitId);

        if (unit is null)
        {
            throw new InvalidOperationException($"وحدة القياس غير موجودة (ID: {unitId})");
        }

        // Base unit has ConversionFactor = 1, so converting to base = quantity * factor
        return MoneyPolicy.RoundToJOD(quantity * unit.ConversionFactor);
    }

    public async Task<decimal> ConvertFromBaseAsync(decimal quantity, Guid unitId)
    {
        var units = await GetAllUnitsAsync();
        var unit = units.FirstOrDefault(u => u.Id == unitId);

        if (unit is null)
        {
            throw new InvalidOperationException($"وحدة القياس غير موجودة (ID: {unitId})");
        }

        if (unit.ConversionFactor == 0m)
        {
            throw new InvalidOperationException($"معامل التحويل لوحدة '{unit.Name}' يساوي صفراً");
        }

        // Convert from base: quantityInUnit = quantity / factor
        return MoneyPolicy.RoundToJOD(quantity / unit.ConversionFactor);
    }

    public async Task<List<UnitOfMeasureDto>> GetAllUnitsAsync()
    {
        if (_cachedUnits is not null)
        {
            return _cachedUnits;
        }

        var units = await _unitOfWork.UnitOfMeasures.FindAsync(u => u.IsActive);
        _cachedUnits = units
            .OrderBy(u => u.Category)
            .ThenBy(u => u.SortOrder)
            .Select(u => new UnitOfMeasureDto(
                u.Id, u.Name, u.ArabicName, u.Symbol, u.ArabicSymbol,
                u.Category, u.ConversionFactor, u.IsBaseUnit, u.DecimalPlaces))
            .ToList();

        return _cachedUnits;
    }

    public async Task<Dictionary<string, List<UnitOfMeasureDto>>> GetUnitsByCategoryAsync()
    {
        var units = await GetAllUnitsAsync();
        return units
            .GroupBy(u => u.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
