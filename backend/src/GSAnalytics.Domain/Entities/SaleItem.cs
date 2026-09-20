namespace GSAnalytics.Domain.Entities;

public class SaleItem
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }

    /// <summary>Money is always decimal, never float/double.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Not persisted — compute Quantity * UnitPrice wherever a line total is needed.</summary>
    public decimal LineTotal => Quantity * UnitPrice;

    public Product? Product { get; set; }
}
