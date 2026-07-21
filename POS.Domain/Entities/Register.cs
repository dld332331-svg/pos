namespace POS.Domain.Entities;

public class Register : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ArabicName { get; set; }
    public string? IPAddress { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}
