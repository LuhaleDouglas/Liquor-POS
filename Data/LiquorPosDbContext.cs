using LiquorPOS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Data;

public class LiquorPosDbContext : DbContext
{
    public LiquorPosDbContext(
        DbContextOptions<LiquorPosDbContext> options)
        : base(options)
    {
    }

    // ============================================================
    // PRODUCTS
    // ============================================================

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<PriceGroup> PriceGroups => Set<PriceGroup>();
    public DbSet<Business> Businesses => Set<Business>();


    // ============================================================
    // STOCK
    // ============================================================

    public DbSet<StockTransaction> StockTransactions
        => Set<StockTransaction>();


    // ============================================================
    // USERS / SECURITY
    // ============================================================

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions
        => Set<RolePermission>();


    // ============================================================
    // POS
    // ============================================================

    public DbSet<POSSession> POSSessions
        => Set<POSSession>();

    public DbSet<POSTerminal> POSTerminals
        => Set<POSTerminal>();


    // ============================================================
    // SALES
    // ============================================================

    public DbSet<Sale> Sales
        => Set<Sale>();

    public DbSet<SaleLine> SaleLines
        => Set<SaleLine>();

    public DbSet<SalePayment> SalePayments
        => Set<SalePayment>();


    // ============================================================
    // PURCHASES
    // ============================================================

    public DbSet<Supplier> Suppliers
        => Set<Supplier>();

    public DbSet<Purchase> Purchases
        => Set<Purchase>();

    public DbSet<PurchaseLine> PurchaseLines
        => Set<PurchaseLine>();


    // ============================================================
    // MODEL CONFIGURATION
    // ============================================================

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        // ========================================================
        // PRODUCTS
        // ========================================================

        modelBuilder.Entity<Product>()
            .ToTable("Products")
            .HasKey(x => x.ProductId);

        modelBuilder.Entity<ProductUnit>()
            .ToTable("ProductUnits")
            .HasKey(x => x.ProductUnitId);

        modelBuilder.Entity<UnitOfMeasure>()
            .ToTable("UnitsOfMeasure")
            .HasKey(x => x.UnitId);

        modelBuilder.Entity<ProductPrice>()
            .ToTable("ProductPrices")
            .HasKey(x => x.ProductPriceId);

        modelBuilder.Entity<PriceGroup>()
            .ToTable("PriceGroups")
            .HasKey(x => x.PriceGroupId);


        // ========================================================
        // STOCK
        // ========================================================

        modelBuilder.Entity<StockTransaction>()
            .ToTable("StockTransactions")
            .HasKey(x => x.StockTransactionId);


        // ========================================================
        // USERS
        // ========================================================

        modelBuilder.Entity<User>()
            .ToTable("Users")
            .HasKey(x => x.UserId);

        modelBuilder.Entity<Role>()
            .ToTable("Roles")
            .HasKey(x => x.RoleId);


        // ========================================================
        // PERMISSIONS
        // ========================================================

        modelBuilder.Entity<Permission>()
            .ToTable("Permissions")
            .HasKey(x => x.PermissionId);

        modelBuilder.Entity<Permission>()
            .Property(x => x.PermissionCode)
            .HasMaxLength(100)
            .IsRequired();

        modelBuilder.Entity<Permission>()
            .Property(x => x.PermissionName)
            .HasMaxLength(150)
            .IsRequired();

        modelBuilder.Entity<Permission>()
            .Property(x => x.Description)
            .HasMaxLength(500);


        // ========================================================
        // ROLE PERMISSIONS
        // ========================================================

        modelBuilder.Entity<RolePermission>()
            .ToTable("RolePermissions");

        modelBuilder.Entity<RolePermission>()
            .HasKey(x => new
            {
                x.RoleId,
                x.PermissionId
            });

        modelBuilder.Entity<RolePermission>()
            .HasOne(x => x.Role)
            .WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.RoleId);

        modelBuilder.Entity<RolePermission>()
            .HasOne(x => x.Permission)
            .WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.PermissionId);


        // ========================================================
        // POS TERMINALS
        // ========================================================

        modelBuilder.Entity<POSTerminal>()
            .ToTable("POSTerminals")
            .HasKey(x => x.POSTerminalId);


        // ========================================================
        // POS SESSIONS
        // ========================================================

        modelBuilder.Entity<POSSession>()
            .ToTable("POSSessions")
            .HasKey(x => x.POSSessionId);

        modelBuilder.Entity<POSSession>()
            .Property(x => x.CashDifference)
            .ValueGeneratedOnAddOrUpdate();


        // ========================================================
        // SALES
        // ========================================================

        modelBuilder.Entity<Sale>()
            .ToTable("Sales")
            .HasKey(x => x.SaleId);

        modelBuilder.Entity<SaleLine>()
            .ToTable("SaleLines")
            .HasKey(x => x.SaleLineId);

        modelBuilder.Entity<SalePayment>()
            .ToTable("SalePayments")
            .HasKey(x => x.SalePaymentId);


        // ========================================================
        // SUPPLIERS
        // ========================================================

        modelBuilder.Entity<Supplier>()
            .ToTable("Suppliers")
            .HasKey(x => x.SupplierId);


        // ========================================================
        // PURCHASES
        // ========================================================

        modelBuilder.Entity<Purchase>()
            .ToTable("Purchases")
            .HasKey(x => x.PurchaseId);


        // ========================================================
        // PURCHASE LINES
        // ========================================================

        modelBuilder.Entity<PurchaseLine>()
            .ToTable("PurchaseLines")
            .HasKey(x => x.PurchaseLineId);


        // ========================================================
        // SUPPLIER → PURCHASES
        // ========================================================

        modelBuilder.Entity<Purchase>()
            .HasOne(x => x.Supplier)
            .WithMany(x => x.Purchases)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);


        // ========================================================
        // PURCHASE → PURCHASE LINES
        // ========================================================

        modelBuilder.Entity<PurchaseLine>()
            .HasOne(x => x.Purchase)
            .WithMany(x => x.PurchaseLines)
            .HasForeignKey(x => x.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);


        // ========================================================
        // PURCHASE LINE → PRODUCT
        // ========================================================

        modelBuilder.Entity<PurchaseLine>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);


        // ========================================================
        // PURCHASE LINE → PRODUCT UNIT
        // ========================================================

        modelBuilder.Entity<PurchaseLine>()
            .HasOne(x => x.ProductUnit)
            .WithMany()
            .HasForeignKey(x => x.ProductUnitId)
            .OnDelete(DeleteBehavior.Restrict);

            // ========================================================
        // BUSINESS
        // ========================================================

        modelBuilder.Entity<Business>()
            .ToTable("Businesses")
            .HasKey(x => x.BusinessId);

        modelBuilder.Entity<Business>()
            .Property(x => x.BusinessName)
            .HasMaxLength(200)
            .IsRequired();

        modelBuilder.Entity<Business>()
            .Property(x => x.KRA_PIN)
            .HasMaxLength(50);

        modelBuilder.Entity<Business>()
            .Property(x => x.Phone)
            .HasMaxLength(50);

        modelBuilder.Entity<Business>()
            .Property(x => x.Email)
            .HasMaxLength(150);

        modelBuilder.Entity<Business>()
            .Property(x => x.Address)
            .HasMaxLength(500);
        modelBuilder.Entity<Business>()
            .Property(x => x.PayBill)
            .HasMaxLength(50);

        modelBuilder.Entity<Business>()
            .Property(x => x.DevelopedBy)
            .HasMaxLength(150);
    }
}

