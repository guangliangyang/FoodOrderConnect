using System.Text.Json;
using BidOne.InternalSystemApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BidOne.InternalSystemApi.Data;

public class BidOneDbContext : DbContext
{
    public BidOneDbContext(DbContextOptions<BidOneDbContext> options) : base(options)
    {
    }

    public DbSet<OrderEntity> Orders { get; set; }
    public DbSet<OrderItemEntity> OrderItems { get; set; }
    public DbSet<CustomerEntity> Customers { get; set; }
    public DbSet<SupplierEntity> Suppliers { get; set; }
    public DbSet<ProductEntity> Products { get; set; }
    public DbSet<InventoryEntity> Inventory { get; set; }
    public DbSet<OrderEventEntity> OrderEvents { get; set; }
    public DbSet<AuditLogEntity> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureOrderEntity(modelBuilder);
        ConfigureOrderItemEntity(modelBuilder);
        ConfigureCustomerEntity(modelBuilder);
        ConfigureSupplierEntity(modelBuilder);
        ConfigureProductEntity(modelBuilder);
        ConfigureInventoryEntity(modelBuilder);
        ConfigureOrderEventEntity(modelBuilder);
        ConfigureAuditLogEntity(modelBuilder);

        SeedData(modelBuilder);
    }

    private static void ConfigureOrderEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.CustomerId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SupplierId).HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();

            // JSON column for metadata
            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new())
                .HasColumnType("nvarchar(max)");

            // Indexes
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.SupplierId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);

            // Relationships
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Supplier)
                .WithMany(s => s.Orders)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureOrderItemEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderItemEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ProductId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.TotalPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(100);

            // JSON column for properties
            entity.Property(e => e.Properties)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new())
                .HasColumnType("nvarchar(max)");

            // Relationships
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCustomerEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired();

            entity.HasIndex(e => e.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        });
    }

    private static void ConfigureSupplierEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SupplierEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContactEmail).HasMaxLength(200);
            entity.Property(e => e.ContactPhone).HasMaxLength(50);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.ApiEndpoint).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired();

            entity.HasIndex(e => e.ContactEmail);
        });
    }

    private static void ConfigureProductEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.SupplierId).HasMaxLength(50);
            entity.Property(e => e.IsActive).IsRequired();

            // Indexes
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.SupplierId);
            entity.HasIndex(e => e.Name);

            // Relationships
            entity.HasOne(e => e.Supplier)
                .WithMany(s => s.Products)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInventoryEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.QuantityOnHand).IsRequired();
            entity.Property(e => e.QuantityReserved).IsRequired();
            entity.Property(e => e.ReorderLevel).IsRequired();
            entity.Property(e => e.LastUpdated).IsRequired();

            // Unique index for ProductId
            entity.HasIndex(e => e.ProductId).IsUnique();

            // Relationships
            entity.HasOne(e => e.Product)
                .WithOne(p => p.Inventory)
                .HasForeignKey<InventoryEntity>(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureOrderEventEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EventData).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(100);

            // Indexes
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.CreatedAt);

            // Relationships
            entity.HasOne<OrderEntity>()
                .WithMany()
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAuditLogEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Changes).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.Timestamp).IsRequired();

            // Indexes
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.Timestamp);
        });
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Seed Suppliers
        modelBuilder.Entity<SupplierEntity>().HasData(
            new SupplierEntity
            {
                Id = "SUPP-001",
                Name = "Fresh Foods Wholesale",
                ContactEmail = "orders@freshfoods.com",
                ContactPhone = "+1-555-0101",
                Address = "123 Wholesale Ave, Food City, FC 12345",
                ApiEndpoint = "https://api.freshfoods.com/v1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new SupplierEntity
            {
                Id = "SUPP-002",
                Name = "Quality Meats Ltd",
                ContactEmail = "supply@qualitymeats.com",
                ContactPhone = "+1-555-0102",
                Address = "456 Butcher Blvd, Meat Town, MT 67890",
                ApiEndpoint = "https://api.qualitymeats.com/orders",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        // Seed Customers
        modelBuilder.Entity<CustomerEntity>().HasData(
            new CustomerEntity
            {
                Id = "CUST-001",
                Name = "Bella's Italian Restaurant",
                Email = "orders@bellas.com",
                Phone = "+1-555-1001",
                Address = "789 Restaurant Row, Food District, FD 11111",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new CustomerEntity
            {
                Id = "CUST-002",
                Name = "The Gourmet Kitchen",
                Email = "purchasing@gourmetkitchen.com",
                Phone = "+1-555-1002",
                Address = "321 Culinary Circle, Chef City, CC 22222",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        // Seed Products
        modelBuilder.Entity<ProductEntity>().HasData(
            new ProductEntity
            {
                Id = "PROD-001",
                Name = "Organic Tomatoes",
                Description = "Fresh organic vine-ripened tomatoes",
                Category = "Vegetables",
                UnitPrice = 3.50m,
                Unit = "lb",
                SupplierId = "SUPP-001",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ProductEntity
            {
                Id = "PROD-002",
                Name = "Premium Ground Beef",
                Description = "85% lean ground beef, grass-fed",
                Category = "Meat",
                UnitPrice = 8.99m,
                Unit = "lb",
                SupplierId = "SUPP-002",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        // Seed Inventory
        modelBuilder.Entity<InventoryEntity>().HasData(
            new InventoryEntity
            {
                Id = Guid.NewGuid(),
                ProductId = "PROD-001",
                QuantityOnHand = 500,
                QuantityReserved = 0,
                ReorderLevel = 50,
                LastUpdated = DateTime.UtcNow
            },
            new InventoryEntity
            {
                Id = Guid.NewGuid(),
                ProductId = "PROD-002",
                QuantityOnHand = 200,
                QuantityReserved = 0,
                ReorderLevel = 25,
                LastUpdated = DateTime.UtcNow
            }
        );
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await AddAuditLogs();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task AddAuditLogs()
    {
        var auditEntries = new List<AuditLogEntity>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLogEntity || entry.State == EntityState.Unchanged)
                continue;

            var auditEntry = new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                EntityType = entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                Timestamp = DateTime.UtcNow,
                UserId = "System" // TODO: Get from current user context
            };

            if (entry.Entity is IEntity entityWithId)
            {
                auditEntry.EntityId = entityWithId.Id?.ToString() ?? string.Empty;
            }

            var changes = new Dictionary<string, object>();

            foreach (var property in entry.Properties)
            {
                if (property.IsTemporary)
                    continue;

                var propertyName = property.Metadata.Name;

                switch (entry.State)
                {
                    case EntityState.Added:
                        changes[propertyName] = new { NewValue = property.CurrentValue };
                        break;
                    case EntityState.Deleted:
                        changes[propertyName] = new { OldValue = property.OriginalValue };
                        break;
                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            changes[propertyName] = new
                            {
                                OldValue = property.OriginalValue,
                                NewValue = property.CurrentValue
                            };
                        }
                        break;
                }
            }

            auditEntry.Changes = JsonSerializer.Serialize(changes);
            auditEntries.Add(auditEntry);
        }

        await AuditLogs.AddRangeAsync(auditEntries);
    }
}
