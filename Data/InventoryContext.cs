// Data/InventoryContext.cs - Enhanced with Workflow Entities
using Microsoft.EntityFrameworkCore;
using InventorySystem.Models;
using InventorySystem.Domain.Entities.Production;
using InventorySystem.Domain.Enums;

namespace InventorySystem.Data
{
  public class InventoryContext : DbContext
  {
    public InventoryContext(DbContextOptions<InventoryContext> options) : base(options)
    {
    }

    // Existing DbSets
    public DbSet<Item> Items { get; set; }
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<PurchaseDocument> PurchaseDocuments { get; set; }
    public DbSet<Bom> Boms { get; set; }
    public DbSet<BomItem> BomItems { get; set; }
    public DbSet<InventoryAdjustment> InventoryAdjustments { get; set; }
    

    public DbSet<FinishedGood> FinishedGoods { get; set; }
    public DbSet<Production> Productions { get; set; }
    public DbSet<ProductionConsumption> ProductionConsumptions { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }
    public DbSet<ChangeOrder> ChangeOrders { get; set; } = null!;
    public DbSet<ChangeOrderDocument> ChangeOrderDocuments { get; set; }
    //public DbSet<SaleDocument> SaleDocuments { get; set; }
    

    // New Workflow DbSets
    public DbSet<ProductionWorkflow> ProductionWorkflows { get; set; }
    public DbSet<WorkflowTransition> WorkflowTransitions { get; set; }

    // Additional DbSet for ItemDocument
    public DbSet<ItemDocument> ItemDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      // Existing model configurations remain the same...
      ConfigureExistingEntities(modelBuilder);

      // Configure new workflow entities
      ConfigureWorkflowEntities(modelBuilder);
    }

    private void ConfigureExistingEntities(ModelBuilder modelBuilder)
    {
      // Item configuration
      modelBuilder.Entity<Item>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.PartNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
        entity.HasIndex(e => e.PartNumber).IsUnique();
      });

      // Purchase configuration
      modelBuilder.Entity<Purchase>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CostPerUnit).HasColumnType("decimal(18,2)");
        entity.Property(e => e.TotalCost).HasColumnType("decimal(18,2)");
        entity.HasOne(p => p.Item)
              .WithMany(i => i.Purchases)
              .HasForeignKey(p => p.ItemId);
      });

      // BOM configuration
      modelBuilder.Entity<Bom>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.BomNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.AssemblyPartNumber).IsRequired().HasMaxLength(100);
        entity.HasIndex(e => e.BomNumber).IsUnique();
      });

      // BOM Item configuration
      modelBuilder.Entity<BomItem>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.HasOne(bi => bi.Bom)
              .WithMany(b => b.BomItems)
              .HasForeignKey(bi => bi.BomId);
        entity.HasOne(bi => bi.Item)
              .WithMany()
              .HasForeignKey(bi => bi.ItemId);
      });

      // Production configuration
      modelBuilder.Entity<Production>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.MaterialCost).HasColumnType("decimal(18,2)");
        entity.Property(e => e.LaborCost).HasColumnType("decimal(18,2)");
        entity.Property(e => e.OverheadCost).HasColumnType("decimal(18,2)");
        entity.HasOne(p => p.FinishedGood)
              .WithMany(fg => fg.Productions)
              .HasForeignKey(p => p.FinishedGoodId);
        entity.HasOne(p => p.Bom)
              .WithMany()
              .HasForeignKey(p => p.BomId);
      });

      // Production Consumption configuration
      modelBuilder.Entity<ProductionConsumption>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UnitCostAtConsumption).HasColumnType("decimal(18,2)");
        entity.HasOne(pc => pc.Production)
              .WithMany(p => p.MaterialConsumptions)
              .HasForeignKey(pc => pc.ProductionId);
        entity.HasOne(pc => pc.Item)
              .WithMany()
              .HasForeignKey(pc => pc.ItemId);
      });

      // Finished Good configuration
      modelBuilder.Entity<FinishedGood>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
        entity.Property(e => e.UnitCost).HasColumnType("decimal(18,2)");
        entity.Property(e => e.SellingPrice).HasColumnType("decimal(18,2)");
        entity.HasOne(fg => fg.Bom)
              .WithMany()
              .HasForeignKey(fg => fg.BomId);
      });

      // Sales configuration
      modelBuilder.Entity<Sale>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SaleNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
        entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
        entity.Property(e => e.ShippingCost).HasColumnType("decimal(18,2)");
        entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
        entity.HasIndex(e => e.SaleNumber).IsUnique();
      });

      // Sale Item configuration
      modelBuilder.Entity<SaleItem>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
        entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
        entity.HasOne(si => si.Sale)
              .WithMany(s => s.SaleItems)
              .HasForeignKey(si => si.SaleId);
        entity.HasOne(si => si.FinishedGood)
              .WithMany()
              .HasForeignKey(si => si.FinishedGoodId);
      });

      // Change Order configuration
      modelBuilder.Entity<ChangeOrder>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ChangeOrderNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
        entity.HasIndex(e => e.ChangeOrderNumber).IsUnique();
      });
    }

    private void ConfigureWorkflowEntities(ModelBuilder modelBuilder)
    {
      // ProductionWorkflow configuration
      modelBuilder.Entity<ProductionWorkflow>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ProductionId).IsRequired();
        entity.Property(e => e.Status)
              .IsRequired()
              .HasConversion<int>();
        entity.Property(e => e.PreviousStatus)
              .HasConversion<int>();
        entity.Property(e => e.Priority)
              .IsRequired()
              .HasConversion<int>()
              .HasDefaultValue(Priority.Normal);
        entity.Property(e => e.AssignedTo).HasMaxLength(100);
        entity.Property(e => e.AssignedBy).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.QualityCheckNotes).HasMaxLength(500);
        entity.Property(e => e.OnHoldReason).HasMaxLength(200);
        entity.Property(e => e.LastModifiedBy).HasMaxLength(100);
        entity.Property(e => e.QualityCheckPassed).HasDefaultValue(true);

        // Relationships
        entity.HasOne(w => w.Production)
              .WithOne()
              .HasForeignKey<ProductionWorkflow>(w => w.ProductionId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(w => w.WorkflowTransitions)
              .WithOne(t => t.ProductionWorkflow)
              .HasForeignKey(t => t.ProductionWorkflowId)
              .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        entity.HasIndex(e => e.ProductionId).IsUnique();
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.AssignedTo);
        entity.HasIndex(e => e.CreatedDate);
        entity.HasIndex(e => e.EstimatedCompletionDate);
      });

      // WorkflowTransition configuration
      modelBuilder.Entity<WorkflowTransition>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ProductionWorkflowId).IsRequired();
        entity.Property(e => e.FromStatus)
              .IsRequired()
              .HasConversion<int>();
        entity.Property(e => e.ToStatus)
              .IsRequired()
              .HasConversion<int>();
        entity.Property(e => e.EventType)
              .IsRequired()
              .HasConversion<int>();
        entity.Property(e => e.TransitionDate).IsRequired();
        entity.Property(e => e.TriggeredBy).HasMaxLength(100);
        entity.Property(e => e.Reason).HasMaxLength(500);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.SystemInfo).HasMaxLength(200);

        // Indexes
        entity.HasIndex(e => e.ProductionWorkflowId);
        entity.HasIndex(e => e.TransitionDate);
        entity.HasIndex(e => e.EventType);
      });
    }

    // Override SaveChanges to handle automatic timestamps
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      var entries = ChangeTracker
          .Entries()
          .Where(e => e.Entity is ProductionWorkflow && (e.State == EntityState.Added || e.State == EntityState.Modified));

      foreach (var entityEntry in entries)
      {
        var workflow = (ProductionWorkflow)entityEntry.Entity;

        if (entityEntry.State == EntityState.Added)
        {
          workflow.CreatedDate = DateTime.UtcNow;
        }

        workflow.LastModifiedDate = DateTime.UtcNow;
      }

      return await base.SaveChangesAsync(cancellationToken);
    }
  }
}