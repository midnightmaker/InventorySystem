using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddIsExpenseField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BomItems_Items_ItemId1",
                table: "BomItems");

            migrationBuilder.DropIndex(
                name: "IX_BomItems_ItemId1",
                table: "BomItems");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ItemId1",
                table: "BomItems");

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsExpense",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CompanyInfo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AddressLine2 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ZipCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Fax = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Website = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LogoData = table.Column<byte[]>(type: "BLOB", nullable: true),
                    LogoContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LogoFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    TaxId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BusinessLicense = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    PrimaryContactName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PrimaryContactTitle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PrimaryContactPhone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyInfo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MobilePhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BillingAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    BillingCity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BillingState = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BillingZipCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    BillingCountry = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ShippingAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ShippingCity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ShippingState = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ShippingZipCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ShippingCountry = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CustomerType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsTaxExempt = table.Column<bool>(type: "INTEGER", nullable: false),
                    TaxExemptId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DefaultPaymentTerms = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ContactPerson = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ContactTitle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    ContactPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PreferredCommunication = table.Column<int>(type: "INTEGER", nullable: false),
                    AcceptsMarketing = table.Column<bool>(type: "INTEGER", nullable: false),
                    PricingTier = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    DocumentData = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerDocuments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDocuments_CustomerId",
                table: "CustomerDocuments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Customers_CustomerId",
                table: "Sales",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Customers_CustomerId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "CompanyInfo");

            migrationBuilder.DropTable(
                name: "CustomerDocuments");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "IsExpense",
                table: "Items");

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Sales",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ItemId1",
                table: "BomItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BomItems_ItemId1",
                table: "BomItems",
                column: "ItemId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BomItems_Items_ItemId1",
                table: "BomItems",
                column: "ItemId1",
                principalTable: "Items",
                principalColumn: "Id");
        }
    }
}
