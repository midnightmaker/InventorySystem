// Data/InventoryContext.cs - Enhanced with Workflow Entities
using InventorySystem.Domain.Entities.Production;
using InventorySystem.Domain.Enums;
using InventorySystem.Models;
using InventorySystem.Models.Accounting;
using InventorySystem.Models.CustomerService;
using InventorySystem.Models.Enums;
using InventorySystem.Services;
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

		// Customers
		public DbSet<Customer> Customers { get; set; }
		public DbSet<CustomerDocument> CustomerDocuments { get; set; }
    public DbSet<CustomerPayment> CustomerPayments { get; set; }

    // Add Projects DbSet for R&D project tracking
    public DbSet<Project> Projects { get; set; }

		// Chart of Accounts
		public DbSet<Account> Accounts { get; set; } = null!;

		// General Ledger Entries for double-entry bookkeeping
		public DbSet<GeneralLedgerEntry> GeneralLedgerEntries { get; set; } = null!;
		// Accounts Payable records
		public DbSet<AccountsPayable> AccountsPayable { get; set; } = null!;

		// Vendor Payment records
		public DbSet<VendorPayment> VendorPayments { get; set; } = null!;
		public DbSet<CustomerBalanceAdjustment> CustomerBalanceAdjustments { get; set; }

		// Service entities
		public DbSet<ServiceOrder> ServiceOrders { get; set; }
		public DbSet<ServiceType> ServiceTypes { get; set; }
		public DbSet<ServiceTimeLog> ServiceTimeLogs { get; set; }
		public DbSet<ServiceMaterial> ServiceMaterials { get; set; }
		public DbSet<ServiceDocument> ServiceDocuments { get; set; }

		// Add these new DbSets to the existing InventoryContext class
		public DbSet<Expense> Expenses { get; set; }
		public DbSet<ExpensePayment> ExpensePayments { get; set; }

		// Add DbSet for ServiceTypeDocument
		public DbSet<ServiceTypeDocument> ServiceTypeDocuments { get; set; }

		public DbSet<Shipment> Shipments { get; set; }
		public DbSet<ShipmentItem> ShipmentItems { get; set; }

		public DbSet<FinancialPeriod> FinancialPeriods { get; set; }
		public DbSet<CompanySettings> CompanySettings { get; set; }

		public DbSet<SupportCase> SupportCases { get; set; }
		public DbSet<CaseUpdate> CaseUpdates { get; set; }
		public DbSet<CaseDocument> CaseDocuments { get; set; }
		public DbSet<CaseEscalation> CaseEscalations { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			// ============= CORE INVENTORY ENTITIES =============

			// Item configuration
			modelBuilder.Entity<Item>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.PartNumber).IsRequired().HasMaxLength(100);
				entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
				entity.HasIndex(e => e.PartNumber).IsUnique();

				// Configure SalePrice as decimal with proper precision
				entity.Property(e => e.SalePrice).HasColumnType("decimal(18,2)");

				// Self-referencing relationships for Item versioning
				entity.HasOne(i => i.BaseItem)
							.WithMany(i => i.Versions)
							.HasForeignKey(i => i.BaseItemId)
							.OnDelete(DeleteBehavior.Restrict);

				// Item relationships - use NoAction to avoid cascade conflicts
				entity.HasMany(i => i.Purchases)
							.WithOne(p => p.Item)
							.HasForeignKey(p => p.ItemId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(i => i.DesignDocuments)
							.WithOne(d => d.Item)
							.HasForeignKey(d => d.ItemId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(i => i.CreatedFromChangeOrder)
							.WithOne(c => c.NewItem)
							.HasForeignKey<Item>(i => i.CreatedFromChangeOrderId)
							.OnDelete(DeleteBehavior.SetNull);

				// VendorItem relationships - NoAction to prevent circular cascades
				entity.HasMany(i => i.VendorItems)
							.WithOne(vi => vi.Item)
							.HasForeignKey(vi => vi.ItemId)
							.OnDelete(DeleteBehavior.NoAction);

			});

			// Vendor configuration
			modelBuilder.Entity<Vendor>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.CompanyName).IsUnique();
				entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(200);
				entity.Property(e => e.ContactName).HasMaxLength(100);
				entity.Property(e => e.ContactEmail).HasMaxLength(150);
				entity.Property(e => e.ContactPhone).HasMaxLength(20);
				

				// Vendor relationships
				entity.HasMany(v => v.VendorItems)
							.WithOne(vi => vi.Vendor)
							.HasForeignKey(vi => vi.VendorId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasMany(v => v.Purchases)
							.WithOne(p => p.Vendor)
							.HasForeignKey(p => p.VendorId)
							.OnDelete(DeleteBehavior.Restrict);
			});

			// VendorItem configuration
			modelBuilder.Entity<VendorItem>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(vi => new { vi.VendorId, vi.ItemId }).IsUnique();

				entity.Property(e => e.VendorPartNumber).HasMaxLength(100);
				entity.Property(e => e.Notes).HasMaxLength(500);
				entity.Property(e => e.UnitCost).HasColumnType("decimal(18,2)");
				entity.Property(e => e.LeadTimeDays).HasDefaultValue(0);

				// VendorItem relationships - explicit configuration
				entity.HasOne(vi => vi.Vendor)
							.WithMany(v => v.VendorItems)
							.HasForeignKey(vi => vi.VendorId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(vi => vi.Item)
							.WithMany(i => i.VendorItems)
							.HasForeignKey(vi => vi.ItemId)
							.OnDelete(DeleteBehavior.NoAction);
			});

			// Purchase configuration
			modelBuilder.Entity<Purchase>(entity =>
			{
				entity.HasKey(e => e.Id);

				// Decimal properties
				entity.Property(p => p.CostPerUnit).HasColumnType("decimal(18,2)").IsRequired();
				entity.Property(p => p.ShippingCost).HasColumnType("decimal(18,2)").HasDefaultValue(0);
				entity.Property(p => p.TaxAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);

				// String properties
				entity.Property(p => p.PurchaseOrderNumber).HasMaxLength(100);
				entity.Property(p => p.Notes).HasMaxLength(1000);
				entity.Property(p => p.ItemVersion).HasMaxLength(10);

				// Enum and required fields
				entity.Property(p => p.Status).HasConversion<int>().HasDefaultValue(PurchaseStatus.Pending);
				entity.Property(p => p.PurchaseDate).IsRequired();
				entity.Property(p => p.QuantityPurchased).IsRequired();
				entity.Property(p => p.RemainingQuantity).IsRequired();
				entity.Property(p => p.CreatedDate).IsRequired();

				// Purchase relationships
				entity.HasOne(p => p.Item)
							.WithMany()
							.HasForeignKey(p => p.ItemId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(p => p.Vendor)
							.WithMany(v => v.Purchases)
							.HasForeignKey(p => p.VendorId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(p => p.Project)
							.WithMany(pr => pr.Purchases)
							.HasForeignKey(p => p.ProjectId)
							.OnDelete(DeleteBehavior.SetNull);

				entity.HasOne(p => p.ItemVersionReference)
							.WithMany()
							.HasForeignKey(p => p.ItemVersionId)
							.OnDelete(DeleteBehavior.SetNull);

				entity.HasMany(p => p.PurchaseDocuments)
							.WithOne(pd => pd.Purchase)
							.HasForeignKey(pd => pd.PurchaseId)
							.OnDelete(DeleteBehavior.NoAction);

				// Indexes for performance
				entity.HasIndex(p => p.ItemId);
				entity.HasIndex(p => p.VendorId);
				entity.HasIndex(p => p.PurchaseDate);
				entity.HasIndex(p => new { p.ItemId, p.PurchaseDate });
			});

			// PurchaseDocument configuration - NoAction to avoid cascade conflicts
			modelBuilder.Entity<PurchaseDocument>(entity =>
			{
				entity.HasKey(e => e.Id);

				// Properties
				entity.Property(e => e.DocumentName).IsRequired().HasMaxLength(200);
				entity.Property(e => e.DocumentType).HasMaxLength(100);
				entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
				entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
				entity.Property(e => e.DocumentData).IsRequired();
				entity.Property(e => e.Description).HasMaxLength(500);

				// All relationships use NoAction to prevent cascade conflicts
				entity.HasOne(d => d.Purchase)
							.WithMany(p => p.PurchaseDocuments)
							.HasForeignKey(d => d.PurchaseId)
							.OnDelete(DeleteBehavior.NoAction)
							.IsRequired(false);

				entity.HasOne(d => d.Expense)
							.WithMany(e => e.Documents)
							.HasForeignKey(d => d.ExpenseId)
							.OnDelete(DeleteBehavior.NoAction)
							.IsRequired(false);

				entity.HasOne(d => d.ExpensePayment)
							.WithMany(ep => ep.Documents)
							.HasForeignKey(d => d.ExpensePaymentId)
							.OnDelete(DeleteBehavior.NoAction)
							.IsRequired(false);

				// Indexes
				entity.HasIndex(e => e.PurchaseId);
				entity.HasIndex(e => e.ExpenseId);
				entity.HasIndex(e => e.ExpensePaymentId);
				entity.HasIndex(e => e.UploadedDate);
			});

			// ============= BOM AND PRODUCTION ENTITIES =============

			// BOM configuration
			modelBuilder.Entity<Bom>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.BomNumber).IsRequired().HasMaxLength(100);
				entity.Property(e => e.AssemblyPartNumber).IsRequired().HasMaxLength(100);
				entity.HasIndex(e => e.BomNumber).IsUnique();

				// Self-referencing relationships
				entity.HasOne(b => b.ParentBom)
							.WithMany(b => b.SubAssemblies)
							.HasForeignKey(b => b.ParentBomId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(b => b.BaseBom)
							.WithMany(b => b.Versions)
							.HasForeignKey(b => b.BaseBomId)
							.OnDelete(DeleteBehavior.Restrict);

				// Change order relationship
				entity.HasOne(b => b.CreatedFromChangeOrder)
							.WithOne(c => c.NewBom)
							.HasForeignKey<Bom>(b => b.CreatedFromChangeOrderId)
							.OnDelete(DeleteBehavior.SetNull);

				entity.HasMany(b => b.Documents)
							.WithOne(d => d.Bom)
							.HasForeignKey(d => d.BomId)
							.OnDelete(DeleteBehavior.Cascade);
			});

			// BomItem configuration
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

				entity.HasOne(p => p.ProductionWorkflow)
							.WithOne(pw => pw.Production)
							.HasForeignKey<ProductionWorkflow>(pw => pw.ProductionId);
			});

			// ProductionConsumption configuration
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

			// FinishedGood configuration
			modelBuilder.Entity<FinishedGood>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.PartNumber).IsUnique();
				entity.Property(e => e.PartNumber).HasMaxLength(50);
				entity.Property(e => e.Description).HasMaxLength(200);

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

			// ============= SALES AND CUSTOMER ENTITIES =============

			// Customer configuration
			modelBuilder.Entity<Customer>(entity =>
			{
				entity.HasKey(c => c.Id);
				entity.HasIndex(c => c.Email).IsUnique();
				entity.Property(c => c.CustomerName).IsRequired().HasMaxLength(200);
				entity.Property(c => c.Email).IsRequired().HasMaxLength(150);
				entity.Property(c => c.CreditLimit).HasColumnType("decimal(18,2)");

				entity.HasMany(c => c.Sales)
							.WithOne(s => s.Customer)
							.HasForeignKey(s => s.CustomerId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(c => c.Documents)
							.WithOne(d => d.Customer)
							.HasForeignKey(d => d.CustomerId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasMany(c => c.BalanceAdjustments)
							.WithOne(ba => ba.Customer)
							.HasForeignKey(ba => ba.CustomerId)
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

			// Sale configuration
			modelBuilder.Entity<Sale>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.SaleNumber).IsRequired().HasMaxLength(100);
				entity.Property(e => e.CustomerId).IsRequired();
				entity.Property(e => e.ShippingCost).HasColumnType("decimal(18,2)");
				entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
				entity.HasIndex(e => e.SaleNumber).IsUnique();

				entity.HasOne(s => s.Customer)
							.WithMany(c => c.Sales)
							.HasForeignKey(s => s.CustomerId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(s => s.SaleItems)
							.WithOne(si => si.Sale)
							.HasForeignKey(si => si.SaleId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasMany(s => s.RelatedAdjustments)
							.WithOne(ra => ra.Sale)
							.HasForeignKey(ra => ra.SaleId)
							.OnDelete(DeleteBehavior.SetNull);
			});

			// SaleItem configuration
			modelBuilder.Entity<SaleItem>(entity =>
			{
				entity.HasKey(e => e.Id);
				
				// Item relationship (optional)
				entity.HasOne(si => si.Item)
					  .WithMany()
					  .HasForeignKey(si => si.ItemId)
					  .OnDelete(DeleteBehavior.Restrict);

				// ServiceType relationship (optional)
				entity.HasOne(si => si.ServiceType)
					  .WithMany()
					  .HasForeignKey(si => si.ServiceTypeId)
					  .OnDelete(DeleteBehavior.Restrict);

				// FinishedGood relationship (optional) - ADDED
				entity.HasOne(si => si.FinishedGood)
					  .WithMany(fg => fg.SaleItems)
					  .HasForeignKey(si => si.FinishedGoodId)
					  .OnDelete(DeleteBehavior.Restrict);

				// Ensure exactly one relationship is set
				entity.HasCheckConstraint("CK_SaleItem_OneEntity", 
					"(ItemId IS NOT NULL AND ServiceTypeId IS NULL AND FinishedGoodId IS NULL) OR " +
					"(ItemId IS NULL AND ServiceTypeId IS NOT NULL AND FinishedGoodId IS NULL) OR " +
					"(ItemId IS NULL AND ServiceTypeId IS NULL AND FinishedGoodId IS NOT NULL)");
			});

			// CustomerPayment configuration
			modelBuilder.Entity<CustomerPayment>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
				entity.Property(e => e.JournalEntryNumber).HasMaxLength(50);
				entity.Property(e => e.IsJournalEntryGenerated).HasDefaultValue(false);

				entity.HasIndex(e => e.JournalEntryNumber);
				entity.HasIndex(e => e.IsJournalEntryGenerated);
			});

			// CustomerBalanceAdjustment configuration
			modelBuilder.Entity<CustomerBalanceAdjustment>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.AdjustmentAmount).HasColumnType("decimal(18,2)").IsRequired();
				entity.Property(e => e.AdjustmentType).HasMaxLength(50).IsRequired();
				entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
				entity.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();

				entity.HasOne(e => e.Customer)
							.WithMany(c => c.BalanceAdjustments)
							.HasForeignKey(e => e.CustomerId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.Sale)
							.WithMany(s => s.RelatedAdjustments)
							.HasForeignKey(e => e.SaleId)
							.OnDelete(DeleteBehavior.SetNull);

				entity.HasIndex(e => e.CustomerId);
				entity.HasIndex(e => e.SaleId);
				entity.HasIndex(e => e.AdjustmentDate);
			});

			// ============= EXPENSE AND ACCOUNTING ENTITIES =============

			// Expense configuration
			modelBuilder.Entity<Expense>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.ExpenseCode).IsUnique();
				entity.Property(e => e.DefaultAmount).HasColumnType("decimal(18,2)");

				entity.HasOne(e => e.DefaultVendor)
							.WithMany()
							.HasForeignKey(e => e.DefaultVendorId)
							.OnDelete(DeleteBehavior.SetNull);

				entity.HasOne(e => e.LedgerAccount)
							.WithMany()
							.HasForeignKey(e => e.LedgerAccountId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(e => e.Payments)
							.WithOne(ep => ep.Expense)
							.HasForeignKey(ep => ep.ExpenseId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasMany(e => e.Documents)
							.WithOne(d => d.Expense)
							.HasForeignKey(d => d.ExpenseId)
							.OnDelete(DeleteBehavior.NoAction);
			});

			// ExpensePayment configuration
			modelBuilder.Entity<ExpensePayment>(entity =>
			{
				entity.HasKey(ep => ep.Id);
				entity.Property(ep => ep.Amount).HasColumnType("decimal(18,2)").IsRequired();
				entity.Property(ep => ep.TaxAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);

				entity.HasOne(ep => ep.Expense)
							.WithMany(e => e.Payments)
							.HasForeignKey(ep => ep.ExpenseId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(ep => ep.Vendor)
							.WithMany()
							.HasForeignKey(ep => ep.VendorId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(ep => ep.Project)
							.WithMany()
							.HasForeignKey(ep => ep.ProjectId)
							.OnDelete(DeleteBehavior.SetNull);

				entity.HasMany(ep => ep.Documents)
							.WithOne(d => d.ExpensePayment)
							.HasForeignKey(d => d.ExpensePaymentId)
							.OnDelete(DeleteBehavior.NoAction);
			});

			// ============= ACCOUNTING ENTITIES =============

			// Account configuration
			modelBuilder.Entity<Account>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.AccountCode).IsUnique();
				entity.Property(e => e.AccountCode).IsRequired().HasMaxLength(10);
				entity.Property(e => e.AccountName).IsRequired().HasMaxLength(100);
				entity.Property(e => e.Description).HasMaxLength(200);
				entity.Property(e => e.CurrentBalance).HasColumnType("decimal(18,2)").HasDefaultValue(0);
				entity.Property(e => e.CreatedBy).HasMaxLength(100);

				entity.HasOne(a => a.ParentAccount)
							.WithMany(a => a.SubAccounts)
							.HasForeignKey(a => a.ParentAccountId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(a => a.LedgerEntries)
							.WithOne(le => le.Account)
							.HasForeignKey(le => le.AccountId)
							.OnDelete(DeleteBehavior.Restrict);
			});

			// GeneralLedgerEntry configuration
			modelBuilder.Entity<GeneralLedgerEntry>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.TransactionNumber).IsRequired().HasMaxLength(50);
				entity.Property(e => e.Description).HasMaxLength(200);
				entity.Property(e => e.DebitAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
				entity.Property(e => e.CreditAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
				entity.Property(e => e.ReferenceType).HasMaxLength(50);
				entity.Property(e => e.CreatedBy).HasMaxLength(100);

				entity.HasIndex(e => e.TransactionDate);
				entity.HasIndex(e => e.TransactionNumber);
				entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId });

				entity.HasOne(le => le.Account)
							.WithMany(a => a.LedgerEntries)
							.HasForeignKey(le => le.AccountId)
							.OnDelete(DeleteBehavior.Restrict);
			});

			// AccountsPayable configuration
			modelBuilder.Entity<AccountsPayable>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
				entity.Property(e => e.InvoiceAmount).HasColumnType("decimal(18,2)");
				entity.Property(e => e.AmountPaid).HasColumnType("decimal(18,2)").HasDefaultValue(0);
				entity.Property(e => e.DiscountTaken).HasColumnType("decimal(18,2)").HasDefaultValue(0);
				entity.Property(e => e.Notes).HasMaxLength(200);
				entity.Property(e => e.CreatedBy).HasMaxLength(100);
				entity.Property(e => e.LastModifiedBy).HasMaxLength(100);

				entity.HasIndex(e => e.InvoiceNumber);
				entity.HasIndex(e => e.DueDate);
				entity.HasIndex(e => e.PaymentStatus);

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

			// VendorPayment configuration
			modelBuilder.Entity<VendorPayment>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.PaymentAmount).HasColumnType("decimal(18,2)");
				entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
				entity.Property(e => e.CheckNumber).HasMaxLength(50);
				entity.Property(e => e.BankAccount).HasMaxLength(50);
				entity.Property(e => e.Notes).HasMaxLength(200);
				entity.Property(e => e.ReferenceNumber).HasMaxLength(50);
				entity.Property(e => e.CreatedBy).HasMaxLength(100);

				entity.HasIndex(e => e.PaymentDate);
				entity.HasIndex(e => e.CheckNumber);

				entity.HasOne(p => p.AccountsPayable)
							.WithMany(ap => ap.Payments)
							.HasForeignKey(p => p.AccountsPayableId)
							.OnDelete(DeleteBehavior.Cascade);
			});

			// ============= PROJECT AND MISCELLANEOUS ENTITIES =============

			// Project configuration
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

				entity.Property(p => p.ProjectType).HasConversion<int>();
				entity.Property(p => p.Status).HasConversion<int>();
				entity.Property(p => p.Priority).HasConversion<int>();

				entity.HasIndex(p => p.ProjectCode).IsUnique();

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

			// ChangeOrder configuration
			modelBuilder.Entity<ChangeOrder>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ChangeOrderNumber).IsRequired().HasMaxLength(100);
				entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
				entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
				entity.HasIndex(e => e.ChangeOrderNumber).IsUnique();

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

			// ============= SERVICE ENTITIES =============

			// ServiceOrder configuration
			modelBuilder.Entity<ServiceOrder>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ServiceOrderNumber).IsRequired().HasMaxLength(50);
				entity.HasIndex(e => e.ServiceOrderNumber).IsUnique();

				entity.HasOne(e => e.Customer)
							.WithMany()
							.HasForeignKey(e => e.CustomerId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(e => e.ServiceType)
							.WithMany(st => st.ServiceOrders)
							.HasForeignKey(e => e.ServiceTypeId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(e => e.Sale)
							.WithMany()
							.HasForeignKey(e => e.SaleId)
							.OnDelete(DeleteBehavior.SetNull);
			});

			// ServiceType configuration
			modelBuilder.Entity<ServiceType>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(100);
				entity.Property(e => e.Description).IsRequired(false).HasMaxLength(500); // Make optional if needed
				entity.HasIndex(e => e.ServiceCode).IsUnique();

				// Optional vendor relationship
				entity.HasOne(e => e.Vendor)
							.WithMany()
							.HasForeignKey(e => e.VendorId)
							.OnDelete(DeleteBehavior.SetNull);

				// Optional service item relationship
				entity.HasOne(e => e.ServiceItem)
							.WithMany()
							.HasForeignKey(e => e.ServiceItemId)
							.OnDelete(DeleteBehavior.SetNull);

				entity.HasMany(st => st.ServiceOrders)
							.WithOne(so => so.ServiceType)
							.HasForeignKey(so => so.ServiceTypeId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(st => st.Documents)
							.WithOne(d => d.ServiceType)
							.HasForeignKey(d => d.ServiceTypeId)
							.OnDelete(DeleteBehavior.Cascade);
			});

			// ServiceTimeLog configuration
			modelBuilder.Entity<ServiceTimeLog>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.ServiceOrder)
							.WithMany(so => so.TimeLogs)
							.HasForeignKey(e => e.ServiceOrderId)
							.OnDelete(DeleteBehavior.Cascade);
			});

			// ServiceMaterial configuration
			modelBuilder.Entity<ServiceMaterial>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.ServiceOrder)
							.WithMany(so => so.Materials)
							.HasForeignKey(e => e.ServiceOrderId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(e => e.Item)
							.WithMany()
							.HasForeignKey(e => e.ItemId)
							.OnDelete(DeleteBehavior.Restrict);
			});

			// ServiceDocument configuration
			modelBuilder.Entity<ServiceDocument>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasOne(e => e.ServiceOrder)
							.WithMany(so => so.Documents)
							.HasForeignKey(e => e.ServiceOrderId)
							.OnDelete(DeleteBehavior.Cascade);
			});

			// ServiceTypeDocument configuration
			modelBuilder.Entity<ServiceTypeDocument>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.DocumentName).IsRequired().HasMaxLength(200);
				entity.Property(e => e.DocumentType).HasMaxLength(100);
				entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
				entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
				entity.Property(e => e.DocumentData).IsRequired();
				entity.Property(e => e.Description).HasMaxLength(500);

				entity.HasOne(e => e.ServiceType)
						.WithMany(st => st.Documents)
						.HasForeignKey(e => e.ServiceTypeId)
						.OnDelete(DeleteBehavior.Cascade);
			});

			// ============= WORKFLOW ENTITIES =============

			// ProductionWorkflow configuration
			modelBuilder.Entity<ProductionWorkflow>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.Property(e => e.ProductionId).IsRequired();
				entity.Property(e => e.Status).IsRequired().HasConversion<int>();
				entity.Property(e => e.PreviousStatus).HasConversion<int>();
				entity.Property(e => e.Priority).IsRequired().HasConversion<int>().HasDefaultValue(Priority.Normal);
				entity.Property(e => e.AssignedTo).HasMaxLength(100);
				entity.Property(e => e.AssignedBy).HasMaxLength(100);
				entity.Property(e => e.Notes).HasMaxLength(500);
				entity.Property(e => e.QualityCheckNotes).HasMaxLength(500);
				entity.Property(e => e.OnHoldReason).HasMaxLength(200);
				entity.Property(e => e.LastModifiedBy).HasMaxLength(100);
				entity.Property(e => e.QualityCheckPassed).HasDefaultValue(true);

				entity.HasOne(w => w.Production)
							.WithOne(p => p.ProductionWorkflow)
							.HasForeignKey<ProductionWorkflow>(w => w.ProductionId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasMany(w => w.WorkflowTransitions)
							.WithOne(t => t.ProductionWorkflow)
							.HasForeignKey(t => t.ProductionWorkflowId)
							.OnDelete(DeleteBehavior.Cascade);

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
				entity.Property(e => e.FromStatus).IsRequired().HasConversion<int>();
				entity.Property(e => e.ToStatus).IsRequired().HasConversion<int>();
				entity.Property(e => e.EventType).IsRequired().HasConversion<int>();
				entity.Property(e => e.TransitionDate).IsRequired();
				entity.Property(e => e.TriggeredBy).HasMaxLength(100);
				entity.Property(e => e.Reason).HasMaxLength(500);
				entity.Property(e => e.Notes).HasMaxLength(1000);
				entity.Property(e => e.SystemInfo).HasMaxLength(200);

				entity.HasIndex(e => e.ProductionWorkflowId);
				entity.HasIndex(e => e.TransitionDate);
				entity.HasIndex(e => e.EventType);
			});

			// ============= NAVIGATION PROPERTIES =============

			// Enable lazy loading for specific navigation properties
			modelBuilder.Entity<Customer>()
					.Navigation(c => c.BalanceAdjustments)
					.EnableLazyLoading();

			modelBuilder.Entity<Sale>()
					.Navigation(s => s.RelatedAdjustments)
					.EnableLazyLoading();

			// Financial Period configuration
			modelBuilder.Entity<FinancialPeriod>(entity =>
			{
				entity.HasIndex(e => new { e.StartDate, e.EndDate });
				entity.HasIndex(e => e.IsCurrentPeriod);
			});

			// Company Settings configuration
			modelBuilder.Entity<CompanySettings>(entity =>
			{
				entity.HasOne(e => e.CurrentFinancialPeriod)
							.WithMany()
							.HasForeignKey(e => e.CurrentFinancialPeriodId)
							.OnDelete(DeleteBehavior.SetNull);
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

				// VendorItem relationships - NoAction to prevent circular cascades
				entity.HasMany(i => i.VendorItems)
							.WithOne(vi => vi.Item)
							.HasForeignKey(vi => vi.ItemId)
							.OnDelete(DeleteBehavior.NoAction);

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

			// SaleItem configuration
			modelBuilder.Entity<SaleItem>(entity =>
			{
				entity.HasKey(e => e.Id);

				entity.Property(e => e.Quantity)
						.IsRequired()
						.HasDefaultValue(1);

				entity.Property(e => e.UnitPrice)
						.IsRequired()
						.HasColumnType("decimal(18,2)");

				entity.Property(e => e.UnitCost)
						.HasColumnType("decimal(18,2)")
						.HasDefaultValue(0);

				entity.Property(e => e.QuantitySold)
						.HasDefaultValue(0);

				entity.Property(e => e.QuantityBackordered)
						.HasDefaultValue(0);

				entity.Property(e => e.SerialNumber)
						.HasMaxLength(100);

				entity.Property(e => e.ModelNumber)
						.HasMaxLength(100);

				// Required relationship with Sale
				entity.HasOne(e => e.Sale)
						.WithMany(s => s.SaleItems)
						.HasForeignKey(e => e.SaleId)
						.OnDelete(DeleteBehavior.Cascade);

				// Optional relationship with Item
				entity.HasOne(e => e.Item)
						.WithMany()
						.HasForeignKey(e => e.ItemId)
						.OnDelete(DeleteBehavior.SetNull);

				// Optional relationship with ServiceType
				entity.HasOne(e => e.ServiceType)
						.WithMany()
						.HasForeignKey(e => e.ServiceTypeId)
						.OnDelete(DeleteBehavior.SetNull);

				// Optional relationship with FinishedGood
				entity.HasOne(e => e.FinishedGood)
						.WithMany(fg => fg.SaleItems)
						.HasForeignKey(e => e.FinishedGoodId)
						.OnDelete(DeleteBehavior.SetNull);

				// Indexes for performance
				entity.HasIndex(e => e.SaleId);
				entity.HasIndex(e => e.ItemId);
				entity.HasIndex(e => e.ServiceTypeId);
				entity.HasIndex(e => e.FinishedGoodId);
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
              .IsRequired();
              

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

			// ✅ FINAL FIX: Configure PurchaseDocument entity with NoAction to avoid all conflicts
			modelBuilder.Entity<PurchaseDocument>(entity =>
			{
				entity.HasKey(e => e.Id);

				// Configure properties explicitly
				entity.Property(e => e.DocumentName).IsRequired().HasMaxLength(200);
				entity.Property(e => e.DocumentType).HasMaxLength(100);
				entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
				entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
				entity.Property(e => e.DocumentData).IsRequired();
				entity.Property(e => e.Description).HasMaxLength(500);

				// Purchase relationship (use NoAction to completely avoid cascade conflicts)
				entity.HasOne(d => d.Purchase)
							.WithMany(p => p.PurchaseDocuments)
							.HasForeignKey(d => d.PurchaseId)
							.OnDelete(DeleteBehavior.NoAction)    // ✅ Changed to NoAction
							.IsRequired(false);

				// Expense relationship (use NoAction to completely avoid cascade conflicts)
				entity.HasOne(d => d.Expense)
							.WithMany(e => e.Documents)
							.HasForeignKey(d => d.ExpenseId)
							.OnDelete(DeleteBehavior.NoAction)    // ✅ Changed to NoAction
							.IsRequired(false);

				// ExpensePayment relationship (use NoAction to completely avoid cascade conflicts)  
				entity.HasOne(d => d.ExpensePayment)
							.WithMany(ep => ep.Documents)
							.HasForeignKey(d => d.ExpensePaymentId)
							.OnDelete(DeleteBehavior.NoAction)    // ✅ Changed to NoAction
							.IsRequired(false);

				// Add indexes for performance
				entity.HasIndex(e => e.PurchaseId);
				entity.HasIndex(e => e.ExpenseId);
				entity.HasIndex(e => e.ExpensePaymentId);
				entity.HasIndex(e => e.UploadedDate);
			});
			// ✅ ADD THESE (without the document relationships):
			// NEW: Expense configuration
			modelBuilder.Entity<Expense>(entity =>
			{
				entity.HasKey(e => e.Id);
				entity.HasIndex(e => e.ExpenseCode).IsUnique();

				entity.Property(e => e.DefaultAmount)
							.HasColumnType("decimal(18,2)");

				entity.HasOne(e => e.DefaultVendor)
							.WithMany()
							.HasForeignKey(e => e.DefaultVendorId)
							.OnDelete(DeleteBehavior.SetNull);

				// Add required LedgerAccount relationship
				entity.HasOne(e => e.LedgerAccount)
							.WithMany()
							.HasForeignKey(e => e.LedgerAccountId)
							.OnDelete(DeleteBehavior.Restrict);
			});

			// NEW: ExpensePayment configuration
			modelBuilder.Entity<ExpensePayment>(entity =>
			{
				entity.HasKey(ep => ep.Id);

				entity.Property(ep => ep.Amount)
							.HasColumnType("decimal(18,2)")
							.IsRequired();

				entity.Property(ep => ep.TaxAmount)
							.HasColumnType("decimal(18,2)")
							.HasDefaultValue(0);

				entity.HasOne(ep => ep.Expense)
							.WithMany(e => e.Payments)
							.HasForeignKey(ep => ep.ExpenseId)
							.OnDelete(DeleteBehavior.Cascade);

				entity.HasOne(ep => ep.Vendor)
							.WithMany()
							.HasForeignKey(ep => ep.VendorId)
							.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(ep => ep.Project)
							.WithMany()
							.HasForeignKey(ep => ep.ProjectId)
							.OnDelete(DeleteBehavior.SetNull);

				// Document relationships are handled in PurchaseDocument configuration
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