using GSAnalytics.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GSAnalytics.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Category).HasMaxLength(100);

        builder.HasIndex(p => p.BusinessId);

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(p => p.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
