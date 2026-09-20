using GSAnalytics.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GSAnalytics.Infrastructure.Persistence.Configurations;

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);

        // LineTotal is a computed C# property (Quantity * UnitPrice), never persisted.
        builder.Ignore(i => i.LineTotal);

        builder.HasIndex(i => i.ProductId);

        // Deleting a Product with sales history must not silently wipe that history — block it instead.
        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
