// Data/InventoryContext.cs - Enhanced with Workflow Entities
using InventorySystem.Domain.Entities.Production;
using InventorySystem.Domain.Enums;
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.Enums;
using Microsoft.EntityFrameworkCore;

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

    // New Workflow DbSets
    public DbSet<ProductionWorkflow> ProductionWorkflows { get; set; }
    public DbSet<WorkflowTransition> WorkflowTransitions { get; set; }

    // Additional DbSet for ItemDocument
    public DbSet<ItemDocument> ItemDocuments { get; set; }

    // Additional DbSet for Vendor and VendorItem
    public DbSet<Vendor> Vendors { get; set; }
    public DbSet<VendorItem> VendorItems { get; set; }

    // Add CompanyInfo
    public DbSet<CompanyInfo> CompanyInfo { get; set; } = null!;

		// Add these new DbSets
		public DbSet<Customer> Customers { get; set; }
		public DbSet<CustomerDocument> CustomerDocuments { get; set; }
    public DbSet<CustomerPayment> CustomerPayments { get; set; }

    // NEW: Add Projects DbSet for R&D project tracking
    public DbSet<Project> Projects { get; set; }

		// ============= NEW ACCOUNTING DBSETS =============

		/// <summary>
		/// Chart of Accounts
		/// </summary>
		public DbSet<Account> Accounts { get; set; } = null!;

		/// <summary>
		/// General Ledger Entries for double-entry bookkeeping
		/// </summary>
		public DbSet<GeneralLedgerEntry> GeneralLedgerEntries { get; set; } = null!;

		/// <summary>
		/// Accounts Payable records
		/// </summary>
		public DbSet<AccountsPayable> AccountsPayable { get; set; } = null!;

		/// <summary>
		/// Vendor Payment records
		/// </summary>
		public DbSet<VendorPayment> VendorPayments { get; set; } = null!;


		protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      // Configure existing entities
      ConfigureExistingEntities(modelBuilder);

      // Configure new workflow entities
      ConfigureWorkflowEntities(modelBuilder);

			// Customer configuration
			modelBuilder.Entity<Customer>(entity =>
			{
				entity.HasKey(c => c.Id);
				entity.HasIndex(c => c.Email).IsUnique();
				entity.Property(c => c.CustomerName).IsRequired().HasMaxLength(200);
				entity.Property(c => c.Email).IsRequired().HasMaxLength(150);
				entity.Property(c => c.CreditLimit).HasColumnType("decimal(18,2)");

				// Configure relationships
				entity.HasMany(c => c.Sales)
							.WithOne(s => s.Customer)
							.HasForeignKey(s => s.CustomerId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(c => c.Documents)
							.WithOne(d => d.Customer)
							.HasForeignKey(d => d.CustomerId)
							.OnDelete(DeleteBehavior.Cascade);
			});

			// CustomerDocument configuration
			modelBuilder.Entity<CustomerDocument>(entity =>
			{
				entity.HasKey(d => d.Id);
				entity.Property(d => d.DocumentName).IsRequired().HasMaxLength(200);
				entity.Property(d => d.DocumentType).IsRequired().HasMaxLength(100);
				entity.Property(d => d.FileName).IsRequired().HasMaxLength(255);
				entity.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
				entity.Property(d => d.DocumentData).IsRequired();
			});

			// NEW: Configure Item -> VendorItem preferred relationship
			modelBuilder.Entity<Item>()
          .HasOne(i => i.PreferredVendorItem)
          .WithMany()
          .HasForeignKey(i => i.PreferredVendorItemId)
          .OnDelete(DeleteBehavior.SetNull);

      // Configure Item -> VendorItem collection relationship  
      modelBuilder.Entity<Item>()
          .HasMany(i => i.VendorItems)
          .WithOne(vi => vi.Item)
          .HasForeignKey(vi => vi.ItemId)
          .OnDelete(DeleteBehavior.Cascade);

      // Ensure VendorItem -> Vendor relationship
      modelBuilder.Entity<VendorItem>()
          .HasOne(vi => vi.Vendor)
          .WithMany(v => v.VendorItems)
          .HasForeignKey(vi => vi.VendorId)
          .OnDelete(DeleteBehavior.Cascade);

      // Configure unique constraint for VendorItem (one relationship per vendor-item pair)
      modelBuilder.Entity<VendorItem>()
          .HasIndex(vi => new { vi.VendorId, vi.ItemId })
          .IsUnique();

      // NEW: Project configuration for R&D tracking
      modelBuilder.Entity<Project>(entity =>
      {
          entity.HasKey(p => p.Id);
          entity.Property(p => p.ProjectCode).IsRequired().HasMaxLength(50);
          entity.Property(p => p.ProjectName).IsRequired().HasMaxLength(200);
          entity.Property(p => p.Description).HasMaxLength(1000);
          entity.Property(p => p.ProjectManager).HasMaxLength(100);
          entity.Property(p => p.Department).HasMaxLength(100);
          entity.Property(p => p.Notes).HasMaxLength(2000);
          entity.Property(p => p.CreatedBy).HasMaxLength(100);
          entity.Property(p => p.LastModifiedBy).HasMaxLength(100);
          entity.Property(p => p.Budget).HasColumnType("decimal(18,2)");
          
          // Configure enums
          entity.Property(p => p.ProjectType).HasConversion<int>();
          entity.Property(p => p.Status).HasConversion<int>();
          entity.Property(p => p.Priority).HasConversion<int>();
          
          // Configure unique constraint on project code
          entity.HasIndex(p => p.ProjectCode).IsUnique();
          
          // Configure relationship with purchases
          entity.HasMany(p => p.Purchases)
                .WithOne(pu => pu.Project)
                .HasForeignKey(pu => pu.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
      });

      // CompanyInfo configuration
      modelBuilder.Entity<CompanyInfo>(entity =>
      {
          entity.HasKey(e => e.Id);
          entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(200);
          entity.Property(e => e.Email).HasMaxLength(200);
          entity.Property(e => e.Website).HasMaxLength(200);
          entity.Property(e => e.LogoContentType).HasMaxLength(100);
          entity.Property(e => e.LogoFileName).HasMaxLength(255);
      });
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

        // Configure SalePrice as decimal with proper precision
        entity.Property(e => e.SalePrice)
              .HasColumnType("decimal(18,2)");

        // Configure self-referencing relationships for Item versioning
        entity.HasOne(i => i.BaseItem)
              .WithMany(i => i.Versions)
              .HasForeignKey(i => i.BaseItemId)
              .OnDelete(DeleteBehavior.Restrict);

        // Configure relationships
        entity.HasMany(i => i.Purchases)
              .WithOne(p => p.Item)
              .HasForeignKey(p => p.ItemId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(i => i.DesignDocuments)
              .WithOne(d => d.Item)
              .HasForeignKey(d => d.ItemId)
              .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship with ChangeOrder for Items
        entity.HasOne(i => i.CreatedFromChangeOrder)
              .WithOne(c => c.NewItem)
              .HasForeignKey<Item>(i => i.CreatedFromChangeOrderId)
              .OnDelete(DeleteBehavior.SetNull);

        // Configure VendorItems relationship
        entity.HasMany(i => i.VendorItems)
              .WithOne(vi => vi.Item)
              .HasForeignKey(vi => vi.ItemId)
              .OnDelete(DeleteBehavior.Cascade);

        // Configure PreferredVendorItem relationship
        entity.HasOne(i => i.PreferredVendorItem)
              .WithMany()
              .HasForeignKey(i => i.PreferredVendorItemId)
              .OnDelete(DeleteBehavior.SetNull);
      });

    
      // BOM configuration with explicit self-referencing relationships
      modelBuilder.Entity<Bom>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.BomNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.AssemblyPartNumber).IsRequired().HasMaxLength(100);
        entity.HasIndex(e => e.BomNumber).IsUnique();

        // Configure self-referencing relationships explicitly
        entity.HasOne(b => b.ParentBom)
              .WithMany(b => b.SubAssemblies)
              .HasForeignKey(b => b.ParentBomId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(b => b.BaseBom)
              .WithMany(b => b.Versions)
              .HasForeignKey(b => b.BaseBomId)
              .OnDelete(DeleteBehavior.Restrict);

        // Configure relationship with ChangeOrder
        entity.HasOne(b => b.CreatedFromChangeOrder)
              .WithOne(c => c.NewBom)
              .HasForeignKey<Bom>(b => b.CreatedFromChangeOrderId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasMany(b => b.Documents)
              .WithOne(d => d.Bom)
              .HasForeignKey(d => d.BomId)
              .OnDelete(DeleteBehavior.Cascade);
      });

      // ChangeOrder configuration with explicit relationships
      modelBuilder.Entity<ChangeOrder>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ChangeOrderNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
        entity.HasIndex(e => e.ChangeOrderNumber).IsUnique();

        // Configure explicit relationships to avoid ambiguity
        entity.HasOne(c => c.BaseBom)
              .WithMany()
              .HasForeignKey(c => c.BaseBomId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(c => c.NewBom)
              .WithOne(b => b.CreatedFromChangeOrder)
              .HasForeignKey<ChangeOrder>(c => c.NewBomId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(c => c.BaseItem)
              .WithMany()
              .HasForeignKey(c => c.BaseItemId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(c => c.NewItem)
              .WithOne(i => i.CreatedFromChangeOrder)
              .HasForeignKey<ChangeOrder>(c => c.NewItemId)
              .OnDelete(DeleteBehavior.Restrict);
      });

      // BOM Item configuration
      modelBuilder.Entity<BomItem>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.HasOne(bi => bi.Bom)
              .WithMany(b => b.BomItems)
              .HasForeignKey(bi => bi.BomId)
              .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(bi => bi.Item)
              .WithMany()
              .HasForeignKey(bi => bi.ItemId)
              .OnDelete(DeleteBehavior.Restrict);
      });

      // ItemDocument configuration
      modelBuilder.Entity<ItemDocument>(entity =>
      {
        entity.HasKey(e => e.Id);

        entity.HasOne(d => d.Item)
              .WithMany(i => i.DesignDocuments)
              .HasForeignKey(d => d.ItemId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(d => d.Bom)
              .WithMany(b => b.Documents)
              .HasForeignKey(d => d.BomId)
              .OnDelete(DeleteBehavior.Cascade);
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
              .HasForeignKey(p => p.FinishedGoodId)
              .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(p => p.Bom)
              .WithMany()
              .HasForeignKey(p => p.BomId)
              .OnDelete(DeleteBehavior.Restrict);
      });

      // Production Consumption configuration
      modelBuilder.Entity<ProductionConsumption>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UnitCostAtConsumption).HasColumnType("decimal(18,2)");
        entity.HasOne(pc => pc.Production)
              .WithMany(p => p.MaterialConsumptions)
              .HasForeignKey(pc => pc.ProductionId)
              .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(pc => pc.Item)
              .WithMany()
              .HasForeignKey(pc => pc.ItemId)
              .OnDelete(DeleteBehavior.Restrict);
      });

      
      // FinishedGood configurations
      modelBuilder.Entity<FinishedGood>(entity =>
      {
        entity.HasIndex(e => e.PartNumber).IsUnique();
        entity.Property(e => e.PartNumber).HasMaxLength(50);
        entity.Property(e => e.Description).HasMaxLength(200);

        // Configure relationships
        entity.HasOne(e => e.Bom)
              .WithMany()
              .HasForeignKey(e => e.BomId)
              .OnDelete(DeleteBehavior.SetNull);

        entity.HasMany(e => e.Productions)
              .WithOne(p => p.FinishedGood)
              .HasForeignKey(p => p.FinishedGoodId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(e => e.SaleItems)
              .WithOne(si => si.FinishedGood)
              .HasForeignKey(si => si.FinishedGoodId)
              .OnDelete(DeleteBehavior.Restrict);
      });

			// Sales configuration
			modelBuilder.Entity<Sale>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.SaleNumber).IsRequired().HasMaxLength(100);
				entity.Property(e => e.CustomerId).IsRequired(); // Clean - only CustomerID required
				entity.Property(e => e.ShippingCost).HasColumnType("decimal(18,2)");
				entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
				entity.HasIndex(e => e.SaleNumber).IsUnique();

				// Configure relationship to Customer
				entity.HasOne(s => s.Customer)
							.WithMany(c => c.Sales)
							.HasForeignKey(s => s.CustomerId)
							.OnDelete(DeleteBehavior.Restrict);
			});

			// Sale Item configuration
			modelBuilder.Entity<SaleItem>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
        entity.HasOne(si => si.Sale)
              .WithMany(s => s.SaleItems)
              .HasForeignKey(si => si.SaleId)
              .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(si => si.FinishedGood)
              .WithMany()
              .HasForeignKey(si => si.FinishedGoodId)
              .OnDelete(DeleteBehavior.Restrict);
      });
      // Vendor unique constraint on company name
      modelBuilder.Entity<Vendor>()
          .HasIndex(v => v.CompanyName)
          .IsUnique()
          .HasDatabaseName("IX_Vendors_CompanyName");

      // VendorItem composite key and relationships
      modelBuilder.Entity<VendorItem>()
          .HasIndex(vi => new { vi.VendorId, vi.ItemId })
          .IsUnique()
          .HasDatabaseName("IX_VendorItems_VendorId_ItemId");

      // VendorItem relationships
      modelBuilder.Entity<VendorItem>()
          .HasOne(vi => vi.Vendor)
          .WithMany(v => v.VendorItems)
          .HasForeignKey(vi => vi.VendorId)
          .OnDelete(DeleteBehavior.Cascade);

      modelBuilder.Entity<VendorItem>()
          .HasOne(vi => vi.Item)
          .WithMany() // Items don't need to track their vendors directly
          .HasForeignKey(vi => vi.ItemId)
          .OnDelete(DeleteBehavior.Cascade);

      // Purchase entity configuration
      modelBuilder.Entity<Purchase>(entity =>
      {
        entity.HasKey(e => e.Id);

        // Item relationship
        entity.HasOne(p => p.Item)
              .WithMany()
              .HasForeignKey(p => p.ItemId)
              .OnDelete(DeleteBehavior.Restrict);

        // Vendor relationship - REQUIRED (not nullable)
        entity.HasOne(p => p.Vendor)
              .WithMany(v => v.Purchases)
              .HasForeignKey(p => p.VendorId)
              .OnDelete(DeleteBehavior.Restrict); // Don't delete purchases if vendor is deleted

        // Configure decimal properties
        entity.Property(p => p.CostPerUnit)
              .HasColumnType("decimal(18,2)")
              .IsRequired();

        entity.Property(p => p.ShippingCost)
              .HasColumnType("decimal(18,2)")
              .HasDefaultValue(0);

        entity.Property(p => p.TaxAmount)
              .HasColumnType("decimal(18,2)")
              .HasDefaultValue(0);

        // Configure string properties
        entity.Property(p => p.PurchaseOrderNumber)
              .HasMaxLength(100);

        entity.Property(p => p.Notes)
              .HasMaxLength(1000);

        entity.Property(p => p.ItemVersion)
              .HasMaxLength(10);

        // Configure enum
        entity.Property(p => p.Status)
              .HasConversion<int>()
              .HasDefaultValue(PurchaseStatus.Pending);

        // Configure required fields
        entity.Property(p => p.PurchaseDate)
              .IsRequired();

        entity.Property(p => p.QuantityPurchased)
              .IsRequired();

        entity.Property(p => p.RemainingQuantity)
              .IsRequired();

        entity.Property(p => p.CreatedDate)
              .IsRequired()
              .HasDefaultValueSql("datetime('now')");

        // ItemVersion relationship (optional)
        entity.HasOne(p => p.ItemVersionReference)
              .WithMany()
              .HasForeignKey(p => p.ItemVersionId)
              .OnDelete(DeleteBehavior.SetNull);

        // Indexes for performance
        entity.HasIndex(p => p.ItemId)
              .HasDatabaseName("IX_Purchases_ItemId");

        entity.HasIndex(p => p.VendorId)
              .HasDatabaseName("IX_Purchases_VendorId");

        entity.HasIndex(p => p.PurchaseDate)
              .HasDatabaseName("IX_Purchases_PurchaseDate");

        entity.HasIndex(p => new { p.ItemId, p.PurchaseDate })
              .HasDatabaseName("IX_Purchases_ItemId_PurchaseDate");
      });
			// ENHANCED CUSTOMER PAYMENT CONFIGURATION
			modelBuilder.Entity<CustomerPayment>(entity =>
			{
				// ... your existing CustomerPayment configuration ...

				// ADD THESE NEW CONFIGURATIONS:
				entity.Property(e => e.JournalEntryNumber)
						.HasMaxLength(50);

				entity.Property(e => e.IsJournalEntryGenerated)
						.HasDefaultValue(false);

				// Add indexes for performance
				entity.HasIndex(e => e.JournalEntryNumber)
						.HasDatabaseName("IX_CustomerPayments_JournalEntryNumber");

				entity.HasIndex(e => e.IsJournalEntryGenerated)
						.HasDatabaseName("IX_CustomerPayments_IsJournalEntryGenerated");
			});


			ConfigureAccountingEntities(modelBuilder);

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

			// Configure the relationship between Production and ProductionWorkflow
			modelBuilder.Entity<Models.Production>()
					.HasOne(p => p.ProductionWorkflow)
					.WithOne(pw => pw.Production)
					.HasForeignKey<Domain.Entities.Production.ProductionWorkflow>(pw => pw.ProductionId);
		}

   
    public static void ConfigureAccountingEntities(ModelBuilder modelBuilder)
    {
      // ============= ACCOUNT CONFIGURATION =============
      modelBuilder.Entity<Account>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.AccountCode).IsUnique();

        entity.Property(e => e.AccountCode)
            .IsRequired()
            .HasMaxLength(10);

        entity.Property(e => e.AccountName)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(e => e.Description)
            .HasMaxLength(200);

        entity.Property(e => e.CurrentBalance)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);

        entity.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        // Self-referencing relationship for account hierarchy
        entity.HasOne(a => a.ParentAccount)
            .WithMany(a => a.SubAccounts)
            .HasForeignKey(a => a.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship with ledger entries
        entity.HasMany(a => a.LedgerEntries)
            .WithOne(le => le.Account)
            .HasForeignKey(le => le.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
      });

      // ============= GENERAL LEDGER ENTRY CONFIGURATION =============
      modelBuilder.Entity<GeneralLedgerEntry>(entity =>
      {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.TransactionNumber)
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.Description)
            .HasMaxLength(200);

        entity.Property(e => e.DebitAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);

        entity.Property(e => e.CreditAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);

        entity.Property(e => e.ReferenceType)
            .HasMaxLength(50);

        entity.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        // Index for performance
        entity.HasIndex(e => e.TransactionDate);
        entity.HasIndex(e => e.TransactionNumber);
        entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId });

        // Relationship with Account
        entity.HasOne(le => le.Account)
            .WithMany(a => a.LedgerEntries)
            .HasForeignKey(le => le.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
      });

      // ============= ACCOUNTS PAYABLE CONFIGURATION =============
      modelBuilder.Entity<AccountsPayable>(entity =>
      {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.InvoiceAmount)
            .HasColumnType("decimal(18,2)");

        entity.Property(e => e.AmountPaid)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);

        entity.Property(e => e.DiscountTaken)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);

        entity.Property(e => e.Notes)
            .HasMaxLength(200);

        entity.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        entity.Property(e => e.LastModifiedBy)
            .HasMaxLength(100);

        // Indexes for performance
        entity.HasIndex(e => e.InvoiceNumber);
        entity.HasIndex(e => e.DueDate);
        entity.HasIndex(e => e.PaymentStatus);

        // Relationships
        entity.HasOne(ap => ap.Vendor)
            .WithMany()
            .HasForeignKey(ap => ap.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(ap => ap.Purchase)
            .WithMany()
            .HasForeignKey(ap => ap.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(ap => ap.Payments)
            .WithOne(p => p.AccountsPayable)
            .HasForeignKey(p => p.AccountsPayableId)
            .OnDelete(DeleteBehavior.Cascade);
      });

      // ============= VENDOR PAYMENT CONFIGURATION =============
      modelBuilder.Entity<VendorPayment>(entity =>
      {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.PaymentAmount)
            .HasColumnType("decimal(18,2)");

        entity.Property(e => e.DiscountAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);

        entity.Property(e => e.CheckNumber)
            .HasMaxLength(50);

        entity.Property(e => e.BankAccount)
            .HasMaxLength(50);

        entity.Property(e => e.Notes)
            .HasMaxLength(200);

        entity.Property(e => e.ReferenceNumber)
            .HasMaxLength(50);

        entity.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        // Indexes
        entity.HasIndex(e => e.PaymentDate);
        entity.HasIndex(e => e.CheckNumber);

        // Relationship
        entity.HasOne(p => p.AccountsPayable)
            .WithMany(ap => ap.Payments)
            .HasForeignKey(p => p.AccountsPayableId)
            .OnDelete(DeleteBehavior.Cascade);
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