namespace POS.Application.DTOs;

public record ModifierGroupDto(
    Guid Id,
    string Name,
    string? ArabicName,
    bool IsRequired,
    int MinSelections,
    int MaxSelections,
    int SortOrder,
    List<ModifierDto> Modifiers);

public record ModifierDto(
    Guid Id,
    string Name,
    string? ArabicName,
    decimal Price,
    List<ModifierSizeDto> Sizes);

public record ModifierSizeDto(
    Guid Id,
    string Name,
    string? ArabicName,
    decimal Price,
    decimal PriceAdjustment);

/// <summary>
/// Result from the modifier selection dialog — 
/// contains the selected modifiers and computed extra cost.
/// </summary>
public record ModifierSelectionResult(
    List<ModifierSelectionDto> Selections,
    decimal TotalExtra,
    string Summary);
