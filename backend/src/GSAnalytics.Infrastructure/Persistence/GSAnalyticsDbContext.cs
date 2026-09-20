using GSAnalytics.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GSAnalytics.Infrastructure.Persistence;

public class GSAnalyticsDbContext(DbContextOptions<GSAnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<DataImport> DataImports => Set<DataImport>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GSAnalyticsDbContext).Assembly);
    }
}
