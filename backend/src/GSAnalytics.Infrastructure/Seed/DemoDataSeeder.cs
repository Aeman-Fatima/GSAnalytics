using GSAnalytics.Application.Security;
using GSAnalytics.Domain.Entities;
using GSAnalytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GSAnalytics.Infrastructure.Seed;

/// <summary>
/// Seeds one fictional business — Harbor Point Wholesale Supply, a small distributor that sells
/// café/restaurant/retail supplies (coffee, packaging, cleaning products) to local shops — with
/// ~12 months of history ending today. The patterns below are deliberately authored, not random,
/// so the analytics/insights pages built in later phases have something real to show:
///
///   - most customers order on a regular cadence ("repeat customers")
///   - Northside Diner's orders shrink in size and frequency over the last ~5 months ("declining customer")
///   - Green Leaf Juice Bar's last order is much later than its historical cadence would predict
///     ("customer ordering later than usual" / needs-attention candidate)
///   - Espresso Beans sell more every month ("growing product")
///   - Paper Straws sell less every month ("declining product")
///   - Nov/Dec are busier and January is slower ("seasonal variation")
///   - The Daily Grind Coffee House and Riverside Restaurant Group order in bulk ("high-value customers")
/// </summary>
public static class DemoDataSeeder
{
    private enum CustomerTier { Normal, HighValue, Declining, LateOrdering }

    private record ProductSeed(string Name, string Category, decimal BasePrice);

    private record CustomerSeed(string Name, string Segment, string City, int CadenceDays, CustomerTier Tier, int[] PreferredProductIndexes);

    // Index 0 (Espresso Beans) grows every month; index 4 (Paper Straws) declines every month.
    private static readonly ProductSeed[] ProductSeeds =
    [
        new("Espresso Beans - 5kg Bag", "Coffee & Beverage", 68.00m),
        new("Disposable Cups - 12oz (Case of 1000)", "Packaging", 42.00m),
        new("Recycled Napkins (Case of 2000)", "Packaging", 28.00m),
        new("Commercial Dish Soap - 5L", "Cleaning Supplies", 19.50m),
        new("Paper Straws (Case of 5000)", "Packaging", 34.00m),
        new("Oat Milk 1L Carton (Case of 12)", "Coffee & Beverage", 31.00m),
        new("Sanitizer Spray 1L (Case of 12)", "Cleaning Supplies", 22.00m),
        new("Compostable To-Go Containers (Case of 500)", "Packaging", 46.00m),
        new("Sugar Packets (Case of 2000)", "Coffee & Beverage", 16.50m)
    ];

    private static readonly CustomerSeed[] CustomerSeeds =
    [
        new("Harbor Point Café", "Café", "Adelaide", 14, CustomerTier.Normal, [0, 1, 2, 5, 8]),
        new("The Daily Grind Coffee House", "Café", "Norwood", 10, CustomerTier.HighValue, [0, 5, 1, 8]),
        new("Seaside Bistro", "Restaurant", "Glenelg", 18, CustomerTier.Normal, [1, 2, 3, 7]),
        new("Corner Deli & Market", "Retail", "Unley", 21, CustomerTier.Normal, [2, 3, 6]),
        new("Northside Diner", "Restaurant", "Prospect", 16, CustomerTier.Declining, [1, 2, 3, 4, 7]),
        new("Green Leaf Juice Bar", "Café", "Semaphore", 20, CustomerTier.LateOrdering, [5, 8, 1]),
        new("Riverside Restaurant Group", "Restaurant", "Henley Beach", 30, CustomerTier.HighValue, [1, 2, 3, 7, 4]),
        new("Sunset Grill", "Restaurant", "Port Adelaide", 20, CustomerTier.Normal, [1, 3, 4, 7]),
        new("Maple & Co. Bakery", "Bakery", "Burnside", 24, CustomerTier.Normal, [8, 2, 5]),
        new("Downtown Deli", "Retail", "Mawson Lakes", 26, CustomerTier.Normal, [2, 3, 6]),
        new("Blue Bottle Café Annex", "Café", "Golden Grove", 28, CustomerTier.Normal, [0, 5, 8]),
        new("Old Town Market", "Retail", "Salisbury", 35, CustomerTier.Normal, [3, 6, 2])
    ];

    public static async Task SeedAsync(GSAnalyticsDbContext db, IPasswordHasher passwordHasher, CancellationToken ct = default)
    {
        if (await db.Businesses.AnyAsync(ct))
        {
            return; // already seeded — idempotent
        }

        var nowUtc = DateTime.UtcNow;
        var windowEnd = DateOnly.FromDateTime(nowUtc);
        var windowStart = new DateOnly(windowEnd.Year, windowEnd.Month, 1).AddMonths(-11);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "demo@harborpointwholesale.test",
            PasswordHash = passwordHasher.Hash("Demo123!"),
            DisplayName = "Jordan Ellis",
            CreatedAtUtc = nowUtc
        };

        var business = new Business
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Name = "Harbor Point Wholesale Supply",
            CreatedAtUtc = nowUtc
        };

        var products = ProductSeeds
            .Select(p => new Product { Id = Guid.NewGuid(), BusinessId = business.Id, Name = p.Name, Category = p.Category, CreatedAtUtc = nowUtc })
            .ToArray();

        var customers = CustomerSeeds
            .Select(c => new Customer { Id = Guid.NewGuid(), BusinessId = business.Id, Name = c.Name, Segment = c.Segment, CreatedAtUtc = nowUtc })
            .ToArray();

        var sales = new List<Sale>();

        for (var customerIndex = 0; customerIndex < CustomerSeeds.Length; customerIndex++)
        {
            var seed = CustomerSeeds[customerIndex];
            var customer = customers[customerIndex];
            var city = seed.City;

            var effectiveWindowEnd = seed.Tier == CustomerTier.LateOrdering
                ? windowEnd.AddDays(-48) // this customer's last order sits ~48 days before "today"
                : windowEnd;

            var orderDate = windowStart.AddDays(customerIndex % 5);
            var orderIndex = 0;

            while (orderDate <= effectiveWindowEnd)
            {
                var monthsElapsed = ((orderDate.Year - windowStart.Year) * 12) + orderDate.Month - windowStart.Month;

                var sale = new Sale
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    CustomerId = customer.Id,
                    SaleDate = orderDate,
                    City = city,
                    CreatedAtUtc = nowUtc,
                    Items = []
                };

                var slots = Math.Min(seed.PreferredProductIndexes.Length, orderIndex % 2 == 0 ? 3 : 2);
                for (var slot = 0; slot < slots; slot++)
                {
                    var productIndex = seed.PreferredProductIndexes[(orderIndex + slot) % seed.PreferredProductIndexes.Length];
                    var quantity = CalculateQuantity(customerIndex, orderIndex, productIndex, monthsElapsed, orderDate.Month, seed.Tier, forceAtLeastOne: slot == 0);
                    if (quantity <= 0)
                    {
                        continue; // e.g. a customer has effectively stopped ordering Paper Straws by this point in the year
                    }

                    sale.Items.Add(new SaleItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = products[productIndex].Id,
                        Quantity = quantity,
                        UnitPrice = ProductSeeds[productIndex].BasePrice
                    });
                }

                if (sale.Items.Count > 0)
                {
                    sales.Add(sale);
                }

                orderIndex++;

                var cadence = seed.Tier == CustomerTier.Declining && monthsElapsed >= 6
                    ? seed.CadenceDays + ((monthsElapsed - 6) * 6) // orders spread further apart as the decline sets in
                    : seed.CadenceDays;
                var jitterDays = (((customerIndex * 7) + (orderIndex * 13)) % 7) - 3; // deterministic +/-3 day jitter
                orderDate = orderDate.AddDays(cadence + jitterDays);
            }
        }

        db.Users.Add(user);
        db.Businesses.Add(business);
        db.Products.AddRange(products);
        db.Customers.AddRange(customers);
        db.Sales.AddRange(sales);

        await db.SaveChangesAsync(ct);
    }

    private static int CalculateQuantity(int customerIndex, int orderIndex, int productIndex, int monthsElapsed, int calendarMonth, CustomerTier tier, bool forceAtLeastOne)
    {
        var baseQuantity = tier == CustomerTier.HighValue ? 3 : 1;
        var variance = (customerIndex + orderIndex + productIndex) % 2; // 0 or 1

        var productTrend = productIndex switch
        {
            0 => 1.0m + (monthsElapsed * 0.06m),                      // Espresso Beans: grows ~6%/month
            4 => Math.Max(0.2m, 1.0m - (monthsElapsed * 0.08m)),      // Paper Straws: declines ~8%/month
            _ => 1.0m
        };

        var decliningCustomerTrend = tier == CustomerTier.Declining && monthsElapsed >= 6
            ? Math.Max(0.25m, 1.0m - ((monthsElapsed - 6) * 0.15m))
            : 1.0m;

        var seasonal = calendarMonth switch
        {
            11 or 12 => 1.25m, // holiday season bump
            1 => 0.85m,        // slow start to the year
            2 => 0.9m,
            6 or 7 => 1.08m,   // winter hot-beverage bump
            _ => 1.0m
        };

        var scaled = (baseQuantity + variance) * productTrend * decliningCustomerTrend * seasonal;
        var quantity = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);

        return forceAtLeastOne ? Math.Max(quantity, 1) : quantity;
    }
}
