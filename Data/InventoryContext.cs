using Microsoft.EntityFrameworkCore;
using InventorySystem.Models;

namespace InventorySystem.Data
{
	public class InventoryContext : DbContext
	{
		public InventoryContext(DbContextOptions<InventoryContext> options) : base(options) { }

		public DbSet<Item> Items { get; set; }
		public DbSet<Purchase> Purchases { get; set; }
		public DbSet<Bom> Boms { get; set; }
		public DbSet<BomItem> BomItems { get; set; }
		public DbSet<ItemDocument> ItemDocuments { get; set; }
		public DbSet<InventoryAdjustment> InventoryAdjustments { get; set; }
		public DbSet<PurchaseDocument> PurchaseDocuments { get; set; } 

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			// Configure Purchase -> Item relationship
			modelBuilder.Entity<Purchase>()
					.HasOne(p => p.Item)
					.WithMany(i => i.Purchases)
					.HasForeignKey(p => p.ItemId);

			// Configure BomItem -> Bom relationship
			modelBuilder.Entity<BomItem>()
					.HasOne(bi => bi.Bom)
					.WithMany(b => b.BomItems)
					.HasForeignKey(bi => bi.BomId);

			// Configure BomItem -> Item relationship
			modelBuilder.Entity<BomItem>()
					.HasOne(bi => bi.Item)
					.WithMany(i => i.BomItems)
					.HasForeignKey(bi => bi.ItemId);

			// Configure hierarchical BOM relationship (for sub-assemblies)
			modelBuilder.Entity<Bom>()
					.HasOne(b => b.ParentBom)
					.WithMany(b => b.SubAssemblies)
					.HasForeignKey(b => b.ParentBomId);

			// Configure ItemDocument -> Item relationship
			modelBuilder.Entity<ItemDocument>()
					.HasOne(d => d.Item)
					.WithMany(i => i.DesignDocuments)
					.HasForeignKey(d => d.ItemId);

			// Configure InventoryAdjustment -> Item relationship
			modelBuilder.Entity<InventoryAdjustment>()
					.HasOne(a => a.Item)
					.WithMany()
					.HasForeignKey(a => a.ItemId);

			// Configure PurchaseDocument -> Purchase relationship
			modelBuilder.Entity<PurchaseDocument>()
					.HasOne(pd => pd.Purchase)
					.WithMany(p => p.PurchaseDocuments)
					.HasForeignKey(pd => pd.PurchaseId);

			// Ensure unique part numbers
			modelBuilder.Entity<Item>()
					.HasIndex(i => i.PartNumber)
					.IsUnique();

			base.OnModelCreating(modelBuilder);
		}
	}
}