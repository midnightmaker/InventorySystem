// Updated Data/InventoryContext.cs
using Microsoft.EntityFrameworkCore;
using InventorySystem.Models;

namespace InventorySystem.Data
{
  public class InventoryContext : DbContext
  {
    public InventoryContext(DbContextOptions<InventoryContext> options) : base(options) { }

    // Existing DbSets
    public DbSet<Item> Items { get; set; }
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<Bom> Boms { get; set; }
    public DbSet<BomItem> BomItems { get; set; }
    public DbSet<ItemDocument> ItemDocuments { get; set; }
    public DbSet<InventoryAdjustment> InventoryAdjustments { get; set; }
    public DbSet<PurchaseDocument> PurchaseDocuments { get; set; }

    // New Sales and Production DbSets
    public DbSet<FinishedGood> FinishedGoods { get; set; }
    public DbSet<Production> Productions { get; set; }
    public DbSet<ProductionConsumption> ProductionConsumptions { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }
    public DbSet<ChangeOrder> ChangeOrders { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      // Existing configurations
      modelBuilder.Entity<Purchase>()
          .HasOne(p => p.Item)
          .WithMany(i => i.Purchases)
          .HasForeignKey(p => p.ItemId);

      modelBuilder.Entity<BomItem>()
          .HasOne(bi => bi.Bom)
          .WithMany(b => b.BomItems)
          .HasForeignKey(bi => bi.BomId);

      modelBuilder.Entity<BomItem>()
          .HasOne(bi => bi.Item)
          .WithMany(i => i.BomItems)
          .HasForeignKey(bi => bi.ItemId);

      modelBuilder.Entity<Bom>()
          .HasOne(b => b.ParentBom)
          .WithMany(b => b.SubAssemblies)
          .HasForeignKey(b => b.ParentBomId);

      modelBuilder.Entity<ItemDocument>()
          .HasOne(d => d.Item)
          .WithMany(i => i.DesignDocuments)
          .HasForeignKey(d => d.ItemId);

      modelBuilder.Entity<InventoryAdjustment>()
          .HasOne(a => a.Item)
          .WithMany()
          .HasForeignKey(a => a.ItemId);

      modelBuilder.Entity<PurchaseDocument>()
          .HasOne(pd => pd.Purchase)
          .WithMany(p => p.PurchaseDocuments)
          .HasForeignKey(pd => pd.PurchaseId);

      // NEW SALES AND PRODUCTION CONFIGURATIONS

      // FinishedGood -> Bom relationship (optional)
      modelBuilder.Entity<FinishedGood>()
          .HasOne(fg => fg.Bom)
          .WithMany()
          .HasForeignKey(fg => fg.BomId)
          .IsRequired(false);

      // Production -> FinishedGood relationship
      modelBuilder.Entity<Production>()
          .HasOne(p => p.FinishedGood)
          .WithMany(fg => fg.Productions)
          .HasForeignKey(p => p.FinishedGoodId);

      // Production -> Bom relationship
      modelBuilder.Entity<Production>()
          .HasOne(p => p.Bom)
          .WithMany()
          .HasForeignKey(p => p.BomId);

      // ProductionConsumption -> Production relationship
      modelBuilder.Entity<ProductionConsumption>()
          .HasOne(pc => pc.Production)
          .WithMany(p => p.MaterialConsumptions)
          .HasForeignKey(pc => pc.ProductionId);

      // ProductionConsumption -> Item relationship
      modelBuilder.Entity<ProductionConsumption>()
          .HasOne(pc => pc.Item)
          .WithMany()
          .HasForeignKey(pc => pc.ItemId);

      // Sale -> SaleItems relationship
      modelBuilder.Entity<SaleItem>()
          .HasOne(si => si.Sale)
          .WithMany(s => s.SaleItems)
          .HasForeignKey(si => si.SaleId);

      // SaleItem -> Item relationship (optional - for selling raw materials)
      modelBuilder.Entity<SaleItem>()
          .HasOne(si => si.Item)
          .WithMany()
          .HasForeignKey(si => si.ItemId)
          .IsRequired(false);

      // SaleItem -> FinishedGood relationship (optional - for selling finished goods)
      modelBuilder.Entity<SaleItem>()
          .HasOne(si => si.FinishedGood)
          .WithMany(fg => fg.SaleItems)
          .HasForeignKey(si => si.FinishedGoodId)
          .IsRequired(false);

      // Indexes for performance
      modelBuilder.Entity<Item>()
          .HasIndex(i => i.PartNumber)
          .IsUnique();

      modelBuilder.Entity<FinishedGood>()
          .HasIndex(fg => fg.PartNumber)
          .IsUnique();

      modelBuilder.Entity<Sale>()
          .HasIndex(s => s.SaleNumber)
          .IsUnique();

      modelBuilder.Entity<Sale>()
          .HasIndex(s => s.SaleDate);

      modelBuilder.Entity<Production>()
          .HasIndex(p => p.ProductionDate);

      // Configure decimal precision
      modelBuilder.Entity<FinishedGood>()
          .Property(fg => fg.UnitCost)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<FinishedGood>()
          .Property(fg => fg.SellingPrice)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<Production>()
          .Property(p => p.MaterialCost)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<Production>()
          .Property(p => p.LaborCost)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<Production>()
          .Property(p => p.OverheadCost)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<ProductionConsumption>()
          .Property(pc => pc.UnitCostAtConsumption)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<Sale>()
          .Property(s => s.Subtotal)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<Sale>()
          .Property(s => s.TaxAmount)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<Sale>()
          .Property(s => s.ShippingCost)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<Sale>()
          .Property(s => s.TotalAmount)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<SaleItem>()
          .Property(si => si.UnitPrice)
          .HasColumnType("decimal(18,2)");

      modelBuilder.Entity<SaleItem>()
          .Property(si => si.UnitCost)
          .HasColumnType("decimal(18,2)");

      // Ensure SaleItem has either ItemId or FinishedGoodId, but not both
      modelBuilder.Entity<SaleItem>()
          .HasCheckConstraint("CK_SaleItem_ItemOrFinishedGood",
              "(ItemId IS NOT NULL AND FinishedGoodId IS NULL) OR (ItemId IS NULL AND FinishedGoodId IS NOT NULL)");

      base.OnModelCreating(modelBuilder);
    }
  }
}