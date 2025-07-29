using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddBomSupportToItemDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Boms",
                newName: "BomNumber");

            migrationBuilder.AddColumn<string>(
                name: "ItemVersion",
                table: "Purchases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemVersionId",
                table: "Purchases",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemVersionReferenceId",
                table: "Purchases",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseItemId",
                table: "Items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedFromChangeOrderId",
                table: "Items",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrentVersion",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VersionHistory",
                table: "Items",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "ItemDocuments",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "BomId",
                table: "ItemDocuments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseBomId",
                table: "Boms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedFromChangeOrderId",
                table: "Boms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrentVersion",
                table: "Boms",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VersionHistory",
                table: "Boms",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChangeOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChangeOrderNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    BaseEntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousVersion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    NewVersion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ImpactAnalysis = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImplementedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImplementedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CancelledDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    BaseItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseBomId = table.Column<int>(type: "INTEGER", nullable: true),
                    NewItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    NewBomId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeOrders_Boms_BaseBomId",
                        column: x => x.BaseBomId,
                        principalTable: "Boms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChangeOrders_Boms_NewBomId",
                        column: x => x.NewBomId,
                        principalTable: "Boms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChangeOrders_Items_BaseItemId",
                        column: x => x.BaseItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChangeOrders_Items_NewItemId",
                        column: x => x.NewItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChangeOrderDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChangeOrderId = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_ChangeOrderDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeOrderDocuments_ChangeOrders_ChangeOrderId",
                        column: x => x.ChangeOrderId,
                        principalTable: "ChangeOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_ItemVersionReferenceId",
                table: "Purchases",
                column: "ItemVersionReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_BaseItemId",
                table: "Items",
                column: "BaseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CreatedFromChangeOrderId",
                table: "Items",
                column: "CreatedFromChangeOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemDocuments_BomId",
                table: "ItemDocuments",
                column: "BomId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ItemDocument_ItemOrBom",
                table: "ItemDocuments",
                sql: "(ItemId IS NOT NULL AND BomId IS NULL) OR (ItemId IS NULL AND BomId IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Boms_BaseBomId",
                table: "Boms",
                column: "BaseBomId");

            migrationBuilder.CreateIndex(
                name: "IX_Boms_CreatedFromChangeOrderId",
                table: "Boms",
                column: "CreatedFromChangeOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrderDocuments_ChangeOrderId",
                table: "ChangeOrderDocuments",
                column: "ChangeOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_BaseBomId",
                table: "ChangeOrders",
                column: "BaseBomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_BaseItemId",
                table: "ChangeOrders",
                column: "BaseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_NewBomId",
                table: "ChangeOrders",
                column: "NewBomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_NewItemId",
                table: "ChangeOrders",
                column: "NewItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Boms_Boms_BaseBomId",
                table: "Boms",
                column: "BaseBomId",
                principalTable: "Boms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Boms_ChangeOrders_CreatedFromChangeOrderId",
                table: "Boms",
                column: "CreatedFromChangeOrderId",
                principalTable: "ChangeOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemDocuments_Boms_BomId",
                table: "ItemDocuments",
                column: "BomId",
                principalTable: "Boms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_Purchases_Items_ItemVersionReferenceId",
                table: "Purchases",
                column: "ItemVersionReferenceId",
                principalTable: "Items",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Boms_Boms_BaseBomId",
                table: "Boms");

            migrationBuilder.DropForeignKey(
                name: "FK_Boms_ChangeOrders_CreatedFromChangeOrderId",
                table: "Boms");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemDocuments_Boms_BomId",
                table: "ItemDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_ChangeOrders_CreatedFromChangeOrderId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Items_BaseItemId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Items_ItemVersionReferenceId",
                table: "Purchases");

            migrationBuilder.DropTable(
                name: "ChangeOrderDocuments");

            migrationBuilder.DropTable(
                name: "ChangeOrders");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_ItemVersionReferenceId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Items_BaseItemId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_CreatedFromChangeOrderId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_ItemDocuments_BomId",
                table: "ItemDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ItemDocument_ItemOrBom",
                table: "ItemDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Boms_BaseBomId",
                table: "Boms");

            migrationBuilder.DropIndex(
                name: "IX_Boms_CreatedFromChangeOrderId",
                table: "Boms");

            migrationBuilder.DropColumn(
                name: "ItemVersion",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "ItemVersionId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "ItemVersionReferenceId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "BaseItemId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CreatedFromChangeOrderId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsCurrentVersion",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "VersionHistory",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "BomId",
                table: "ItemDocuments");

            migrationBuilder.DropColumn(
                name: "BaseBomId",
                table: "Boms");

            migrationBuilder.DropColumn(
                name: "CreatedFromChangeOrderId",
                table: "Boms");

            migrationBuilder.DropColumn(
                name: "IsCurrentVersion",
                table: "Boms");

            migrationBuilder.DropColumn(
                name: "VersionHistory",
                table: "Boms");

            migrationBuilder.RenameColumn(
                name: "BomNumber",
                table: "Boms",
                newName: "Name");

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "ItemDocuments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
