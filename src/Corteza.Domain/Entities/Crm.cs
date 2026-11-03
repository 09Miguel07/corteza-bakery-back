namespace Corteza.Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string Code { get; set; } = null!; // CLI-001, lo genera una secuencia
    public string DocumentNumber { get; set; } = null!; // inmutable después de crear (pantalla 01.3)
    public string FirstNames { get; set; } = null!;
    public string LastNames { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public DateOnly? Birthday { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<SalesInvoice> SalesInvoices { get; set; } = [];

    public string FullName => $"{FirstNames} {LastNames}";
}
