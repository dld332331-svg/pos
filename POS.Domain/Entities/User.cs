using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ArabicName { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? Pin { get; set; }
    public string? PinHash { get; set; }
    public bool MustChangePassword { get; set; }

    // Navigation
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}
