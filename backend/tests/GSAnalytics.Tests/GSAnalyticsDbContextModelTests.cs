using GSAnalytics.Domain.Entities;
using GSAnalytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GSAnalytics.Tests;

/// <summary>
/// Checks the EF Core model shape directly — no database connection needed — so this stays fast
/// and independent of whether a local Postgres instance is available.
/// </summary>
public class GSAnalyticsDbContextModelTests
{
    private static GSAnalyticsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GSAnalyticsDbContext>()
            .UseNpgsql("Host=localhost;Database=model-validation-only")
            .Options;
        return new GSAnalyticsDbContext(options);
    }

    [Fact]
    public void Model_RegistersAllExpectedEntities()
    {
        using var db = CreateContext();

        var entityTypes = db.Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();

        Assert.Contains(typeof(User), entityTypes);
        Assert.Contains(typeof(Business), entityTypes);
        Assert.Contains(typeof(Customer), entityTypes);
        Assert.Contains(typeof(Product), entityTypes);
        Assert.Contains(typeof(Sale), entityTypes);
        Assert.Contains(typeof(SaleItem), entityTypes);
        Assert.Contains(typeof(DataImport), entityTypes);
    }

    [Fact]
    public void SaleItem_LineTotal_IsNotAPersistedColumn()
    {
        using var db = CreateContext();

        var saleItemType = db.Model.FindEntityType(typeof(SaleItem));
        Assert.NotNull(saleItemType);
        Assert.Null(saleItemType!.FindProperty(nameof(SaleItem.LineTotal)));
    }

    [Fact]
    public void Business_OwnerUserId_HasUniqueIndex()
    {
        using var db = CreateContext();

        var businessType = db.Model.FindEntityType(typeof(Business));
        Assert.NotNull(businessType);
        var index = businessType!.GetIndexes().Single(i => i.Properties.Single().Name == nameof(Business.OwnerUserId));
        Assert.True(index.IsUnique);
    }
}
