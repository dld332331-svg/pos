namespace POS.Domain.Enums;

/// <summary>
/// Specifies the hardware type of a printer.
/// </summary>
public enum PrinterType
{
    /// <summary>Thermal receipt printer using heat-sensitive paper.</summary>
    Thermal,

    /// <summary>Impact dot matrix printer using pins and ribbon.</summary>
    DotMatrix
}