namespace POS.Domain.Enums;

/// <summary>
/// Represents the operational status of a cash register shift.
/// </summary>
public enum ShiftStatus
{
    /// <summary>Shift is currently open and active.</summary>
    Open,

    /// <summary>Shift has been closed and balanced.</summary>
    Closed
}