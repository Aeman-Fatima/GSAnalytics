using GSAnalytics.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GSAnalytics.Infrastructure.Persistence.Configurations;

public class DataImportConfiguration : IEntityTypeConfiguration<DataImport>
{
    public void Configure(EntityTypeBuilder<DataImport> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName).HasMaxLength(300).IsRequired();
        builder.Property(d => d.FileHash).HasMaxLength(64).IsRequired(); // SHA-256 hex = 64 chars
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        // Lets the importer cheaply check "has this exact file already been imported for this business?"
        builder.HasIndex(d => new { d.BusinessId, d.FileHash });

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(d => d.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
