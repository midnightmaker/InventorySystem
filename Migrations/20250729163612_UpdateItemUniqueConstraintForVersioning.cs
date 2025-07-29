using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventorySystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateItemUniqueConstraintForVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_PartNumber",
                table: "Items");

            migrationBuilder.CreateIndex(
                name: "IX_Items_PartNumber_Version",
                table: "Items",
                columns: new[] { "PartNumber", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Boms_BomNumber_Version",
                table: "Boms",
                columns: new[] { "BomNumber", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_PartNumber_Version",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Boms_BomNumber_Version",
                table: "Boms");

            migrationBuilder.CreateIndex(
                name: "IX_Items_PartNumber",
                table: "Items",
                column: "PartNumber",
                unique: true);
        }
    }
}
