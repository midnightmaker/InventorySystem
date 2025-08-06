using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BomItems_Items_ItemId",
                table: "BomItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Boms_Boms_BaseBomId",
                table: "Boms");

            migrationBuilder.DropForeignKey(
                name: "FK_Boms_Boms_ParentBomId",
                table: "Boms");

            migrationBuilder.DropForeignKey(
                name: "FK_Boms_ChangeOrders_CreatedFromChangeOrderId",
                table: "Boms");

            migrationBuilder.DropForeignKey(
                name: "FK_FinishedGoods_Boms_BomId",
                table: "FinishedGoods");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_ChangeOrders_CreatedFromChangeOrderId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Items_BaseItemId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionConsumptions_Items_ItemId",
                table: "ProductionConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Productions_Boms_BomId",
                table: "Productions");

            migrationBuilder.DropForeignKey(
                name: "FK_Productions_FinishedGoods_FinishedGoodId",
                table: "Productions");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Items_ItemId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Items_ItemVersionReferenceId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_FinishedGoods_FinishedGoodId",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_Sales_SaleDate",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleItem_ItemOrFinishedGood",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_Productions_ProductionDate",
                table: "Productions");

            migrationBuilder.DropIndex(
                name: "IX_Items_CreatedFromChangeOrderId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_PartNumber_Version",
                table: "Items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemDocument_ItemOrBom",
                table: "ItemDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ChangeOrders_NewBomId",
                table: "ChangeOrders");

            migrationBuilder.DropIndex(
                name: "IX_ChangeOrders_NewItemId",
                table: "ChangeOrders");

            migrationBuilder.DropIndex(
                name: "IX_Boms_BomNumber_Version",
                table: "Boms");

            migrationBuilder.DropIndex(
                name: "IX_Boms_CreatedFromChangeOrderId",
                table: "Boms");

            migrationBuilder.DropColumn(
                name: "Vendor",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Productions");

            migrationBuilder.DropColumn(
                name: "PreferredVendor",
                table: "Items");

            migrationBuilder.RenameColumn(
                name: "ItemVersionReferenceId",
                table: "Purchases",
                newName: "ItemId1");

            migrationBuilder.RenameIndex(
                name: "IX_Purchases_ItemVersionReferenceId",
                table: "Purchases",
                newName: "IX_Purchases_ItemId1");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDueDate",
                table: "Sales",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Terms",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FinishedGoodId1",
                table: "SaleItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityBackordered",
                table: "SaleItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxAmount",
                table: "Purchases",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ShippingCost",
                table: "Purchases",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                table: "Purchases",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "datetime('now')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualDeliveryDate",
                table: "Purchases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedDeliveryDate",
                table: "Purchases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Purchases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VendorId",
                table: "Purchases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityConsumed",
                table: "ProductionConsumptions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "MaterialType",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentRawMaterialId",
                table: "Items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreferredVendorItemId",
                table: "Items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitOfMeasure",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WastePercentage",
                table: "Items",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "YieldFactor",
                table: "Items",
                type: "decimal(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "FinishedGoods",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "FinishedGoods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "FinishedGoods",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "ChangeOrders",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssemblyPartNumber",
                table: "Boms",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemId1",
                table: "BomItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductionWorkflows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    AssignedTo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AssignedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EstimatedCompletionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    QualityCheckNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    QualityCheckPassed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    QualityCheckerId = table.Column<int>(type: "INTEGER", nullable: true),
                    QualityCheckDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OnHoldReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionWorkflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionWorkflows_Productions_ProductionId",
                        column: x => x.ProductionId,
                        principalTable: "Productions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    VendorCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContactName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Website = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AddressLine1 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TaxId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PaymentTerms = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DiscountPercentage = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPreferred = table.Column<bool>(type: "INTEGER", nullable: false),
                    QualityRating = table.Column<int>(type: "INTEGER", nullable: false),
                    DeliveryRating = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceRating = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductionWorkflowId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ToStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    TransitionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TriggeredBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DurationInMinutes = table.Column<decimal>(type: "TEXT", nullable: true),
                    SystemInfo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_ProductionWorkflows_ProductionWorkflowId",
                        column: x => x.ProductionWorkflowId,
                        principalTable: "ProductionWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VendorId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    VendorPartNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    MinimumOrderQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastPurchaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastPurchaseCost = table.Column<decimal>(type: "decimal(18,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VendorItems_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_FinishedGoodId1",
                table: "SaleItems",
                column: "FinishedGoodId1");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_ItemId_PurchaseDate",
                table: "Purchases",
                columns: new[] { "ItemId", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_ItemVersionId",
                table: "Purchases",
                column: "ItemVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_PurchaseDate",
                table: "Purchases",
                column: "PurchaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_VendorId",
                table: "Purchases",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ParentRawMaterialId",
                table: "Items",
                column: "ParentRawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_PartNumber",
                table: "Items",
                column: "PartNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_PreferredVendorItemId",
                table: "Items",
                column: "PreferredVendorItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_ChangeOrderNumber",
                table: "ChangeOrders",
                column: "ChangeOrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_NewBomId",
                table: "ChangeOrders",
                column: "NewBomId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_NewItemId",
                table: "ChangeOrders",
                column: "NewItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Boms_BomNumber",
                table: "Boms",
                column: "BomNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BomItems_ItemId1",
                table: "BomItems",
                column: "ItemId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkflows_AssignedTo",
                table: "ProductionWorkflows",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkflows_CreatedDate",
                table: "ProductionWorkflows",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkflows_EstimatedCompletionDate",
                table: "ProductionWorkflows",
                column: "EstimatedCompletionDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkflows_ProductionId",
                table: "ProductionWorkflows",
                column: "ProductionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkflows_Status",
                table: "ProductionWorkflows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VendorItems_ItemId",
                table: "VendorItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorItems_VendorId_ItemId",
                table: "VendorItems",
                columns: new[] { "VendorId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CompanyName",
                table: "Vendors",
                column: "CompanyName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_EventType",
                table: "WorkflowTransitions",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_ProductionWorkflowId",
                table: "WorkflowTransitions",
                column: "ProductionWorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_TransitionDate",
                table: "WorkflowTransitions",
                column: "TransitionDate");

            migrationBuilder.AddForeignKey(
                name: "FK_BomItems_Items_ItemId",
                table: "BomItems",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BomItems_Items_ItemId1",
                table: "BomItems",
                column: "ItemId1",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Boms_Boms_BaseBomId",
                table: "Boms",
                column: "BaseBomId",
                principalTable: "Boms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Boms_Boms_ParentBomId",
                table: "Boms",
                column: "ParentBomId",
                principalTable: "Boms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinishedGoods_Boms_BomId",
                table: "FinishedGoods",
                column: "BomId",
                principalTable: "Boms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Items_BaseItemId",
                table: "Items",
                column: "BaseItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Items_ParentRawMaterialId",
                table: "Items",
                column: "ParentRawMaterialId",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_VendorItems_PreferredVendorItemId",
                table: "Items",
                column: "PreferredVendorItemId",
                principalTable: "VendorItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionConsumptions_Items_ItemId",
                table: "ProductionConsumptions",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Productions_Boms_BomId",
                table: "Productions",
                column: "BomId",
                principalTable: "Boms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Productions_FinishedGoods_FinishedGoodId",
                table: "Productions",
                column: "FinishedGoodId",
                principalTable: "FinishedGoods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Items_ItemId",
                table: "Purchases",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Items_ItemId1",
                table: "Purchases",
                column: "ItemId1",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Items_ItemVersionId",
                table: "Purchases",
                column: "ItemVersionId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Vendors_VendorId",
                table: "Purchases",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_FinishedGoods_FinishedGoodId",
                table: "SaleItems",
                column: "FinishedGoodId",
                principalTable: "FinishedGoods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_FinishedGoods_FinishedGoodId1",
                table: "SaleItems",
                column: "FinishedGoodId1",
                principalTable: "FinishedGoods",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BomItems_Items_ItemId",
                table: "BomItems");

            migrationBuilder.DropForeignKey(
                name: "FK_BomItems_Items_ItemId1",
                table: "BomItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Boms_Boms_BaseBomId",
                table: "Boms");

            migrationBuilder.DropForeignKey(
                name: "FK_Boms_Boms_ParentBomId",
                table: "Boms");

            migrationBuilder.DropForeignKey(
                name: "FK_FinishedGoods_Boms_BomId",
                table: "FinishedGoods");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Items_BaseItemId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Items_ParentRawMaterialId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_VendorItems_PreferredVendorItemId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionConsumptions_Items_ItemId",
                table: "ProductionConsumptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Productions_Boms_BomId",
                table: "Productions");

            migrationBuilder.DropForeignKey(
                name: "FK_Productions_FinishedGoods_FinishedGoodId",
                table: "Productions");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Items_ItemId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Items_ItemId1",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Items_ItemVersionId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Vendors_VendorId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_FinishedGoods_FinishedGoodId",
                table: "SaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_FinishedGoods_FinishedGoodId1",
                table: "SaleItems");

            migrationBuilder.DropTable(
                name: "VendorItems");

            migrationBuilder.DropTable(
                name: "WorkflowTransitions");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropTable(
                name: "ProductionWorkflows");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_FinishedGoodId1",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_ItemId_PurchaseDate",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_ItemVersionId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_PurchaseDate",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_VendorId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Items_ParentRawMaterialId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_PartNumber",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_PreferredVendorItemId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_ChangeOrders_ChangeOrderNumber",
                table: "ChangeOrders");

            migrationBuilder.DropIndex(
                name: "IX_ChangeOrders_NewBomId",
                table: "ChangeOrders");

            migrationBuilder.DropIndex(
                name: "IX_ChangeOrders_NewItemId",
                table: "ChangeOrders");

            migrationBuilder.DropIndex(
                name: "IX_Boms_BomNumber",
                table: "Boms");

            migrationBuilder.DropIndex(
                name: "IX_BomItems_ItemId1",
                table: "BomItems");

            migrationBuilder.DropColumn(
                name: "PaymentDueDate",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Terms",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "FinishedGoodId1",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "QuantityBackordered",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ActualDeliveryDate",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "ExpectedDeliveryDate",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "MaterialType",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ParentRawMaterialId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PreferredVendorItemId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasure",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "WastePercentage",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "YieldFactor",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "FinishedGoods");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "FinishedGoods");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "FinishedGoods");

            migrationBuilder.DropColumn(
                name: "ItemId1",
                table: "BomItems");

            migrationBuilder.RenameColumn(
                name: "ItemId1",
                table: "Purchases",
                newName: "ItemVersionReferenceId");

            migrationBuilder.RenameIndex(
                name: "IX_Purchases_ItemId1",
                table: "Purchases",
                newName: "IX_Purchases_ItemVersionReferenceId");

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxAmount",
                table: "Purchases",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "ShippingCost",
                table: "Purchases",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                table: "Purchases",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "datetime('now')");

            migrationBuilder.AddColumn<string>(
                name: "Vendor",
                table: "Purchases",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Productions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "QuantityConsumed",
                table: "ProductionConsumptions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "PreferredVendor",
                table: "Items",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "ChangeOrders",
                type: "TEXT",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "AssemblyPartNumber",
                table: "Boms",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SaleDate",
                table: "Sales",
                column: "SaleDate");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleItem_ItemOrFinishedGood",
                table: "SaleItems",
                sql: "(ItemId IS NOT NULL AND FinishedGoodId IS NULL) OR (ItemId IS NULL AND FinishedGoodId IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Productions_ProductionDate",
                table: "Productions",
                column: "ProductionDate");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CreatedFromChangeOrderId",
                table: "Items",
                column: "CreatedFromChangeOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_PartNumber_Version",
                table: "Items",
                columns: new[] { "PartNumber", "Version" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemDocument_ItemOrBom",
                table: "ItemDocuments",
                sql: "(ItemId IS NOT NULL AND BomId IS NULL) OR (ItemId IS NULL AND BomId IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_NewBomId",
                table: "ChangeOrders",
                column: "NewBomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_NewItemId",
                table: "ChangeOrders",
                column: "NewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Boms_BomNumber_Version",
                table: "Boms",
                columns: new[] { "BomNumber", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Boms_CreatedFromChangeOrderId",
                table: "Boms",
                column: "CreatedFromChangeOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_BomItems_Items_ItemId",
                table: "BomItems",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boms_Boms_BaseBomId",
                table: "Boms",
                column: "BaseBomId",
                principalTable: "Boms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Boms_Boms_ParentBomId",
                table: "Boms",
                column: "ParentBomId",
                principalTable: "Boms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Boms_ChangeOrders_CreatedFromChangeOrderId",
                table: "Boms",
                column: "CreatedFromChangeOrderId",
                principalTable: "ChangeOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FinishedGoods_Boms_BomId",
                table: "FinishedGoods",
                column: "BomId",
                principalTable: "Boms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_ChangeOrders_CreatedFromChangeOrderId",
                table: "Items",
                column: "CreatedFromChangeOrderId",
                principalTable: "ChangeOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Items_BaseItemId",
                table: "Items",
                column: "BaseItemId",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionConsumptions_Items_ItemId",
                table: "ProductionConsumptions",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Productions_Boms_BomId",
                table: "Productions",
                column: "BomId",
                principalTable: "Boms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Productions_FinishedGoods_FinishedGoodId",
                table: "Productions",
                column: "FinishedGoodId",
                principalTable: "FinishedGoods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Items_ItemId",
                table: "Purchases",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Items_ItemVersionReferenceId",
                table: "Purchases",
                column: "ItemVersionReferenceId",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_FinishedGoods_FinishedGoodId",
                table: "SaleItems",
                column: "FinishedGoodId",
                principalTable: "FinishedGoods",
                principalColumn: "Id");
        }
    }
}
