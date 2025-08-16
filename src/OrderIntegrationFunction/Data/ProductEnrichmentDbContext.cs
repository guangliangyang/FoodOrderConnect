using Microsoft.EntityFrameworkCore;

namespace BidOne.OrderIntegrationFunction.Services;

public class ProductEnrichmentDbContext : DbContext
{
    public ProductEnrichmentDbContext(DbContextOptions<ProductEnrichmentDbContext> options) : base(options)
    {
    }

    public DbSet<ProductEnrichmentData> ProductEnrichmentData { get; set; } = null!;
    public DbSet<CustomerEnrichmentData> CustomerEnrichmentData { get; set; } = null!;
    public DbSet<SupplierData> SupplierData { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure for Cosmos DB
        modelBuilder.HasDefaultContainer("OrderEnrichment");

        // ProductEnrichmentData entity configuration
        modelBuilder.Entity<ProductEnrichmentData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.ProductId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Supplier).HasMaxLength(100);
            entity.Property(e => e.Weight).HasPrecision(18, 3);
            entity.Property(e => e.WeightUnit).HasMaxLength(10);
            entity.Property(e => e.LeadTimeDays);
            entity.OwnsOne(e => e.NutritionalInfo);
            entity.Property(e => e.Allergens).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            entity.Property(e => e.LastUpdated);

            entity.HasPartitionKey(e => e.ProductId);
        });

        // CustomerEnrichmentData entity configuration
        modelBuilder.Entity<CustomerEnrichmentData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CustomerId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.PreferredDeliveryMethod).HasMaxLength(50);
            entity.Property(e => e.CreditLimit).HasPrecision(18, 2);
            entity.Property(e => e.CurrentBalance).HasPrecision(18, 2);
            entity.Property(e => e.CustomerTier).HasMaxLength(20);
            entity.Property(e => e.PreferredProducts).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            entity.Property(e => e.LastUpdated);

            entity.HasPartitionKey(e => e.CustomerId);
        });

        // SupplierData entity configuration
        modelBuilder.Entity<SupplierData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.ContactPerson).HasMaxLength(100);
            entity.Property(e => e.IsActive);
            entity.Property(e => e.Products).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            entity.Property(e => e.LastUpdated);

            entity.HasPartitionKey(e => e.Name);
        });
    }
}

public class ProductEnrichmentData
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string WeightUnit { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
    public List<string> Allergens { get; set; } = new();
    public NutritionalInfo NutritionalInfo { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class CustomerEnrichmentData
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PreferredDeliveryMethod { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public string CustomerTier { get; set; } = string.Empty;
    public List<string> PreferredProducts { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class SupplierData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<string> Products { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

[Owned]
public class NutritionalInfo
{
    public int Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
    public decimal Fiber { get; set; }
    public decimal Sugar { get; set; }
    public decimal Sodium { get; set; }
}
