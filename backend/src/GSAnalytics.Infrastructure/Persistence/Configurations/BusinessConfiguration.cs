using GSAnalytics.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GSAnalytics.Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();

        // One business per owning user in V1. This is a business rule for V1, not a structural
        // limitation: later, other users can gain access to a Business via an additive
        // BusinessMembership join table without touching this column or its data.
        builder.HasIndex(b => b.OwnerUserId).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(b => b.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
