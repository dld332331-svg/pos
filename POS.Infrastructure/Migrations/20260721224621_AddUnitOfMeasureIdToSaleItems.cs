using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitOfMeasureIdToSaleItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DisplayQuantity",
                table: "SaleItems",
                type: "DECIMAL(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitOfMeasureId",
                table: "SaleItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_UnitOfMeasureId",
                table: "SaleItems",
                column: "UnitOfMeasureId");

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_UnitOfMeasures_UnitOfMeasureId",
                table: "SaleItems",
                column: "UnitOfMeasureId",
                principalTable: "UnitOfMeasures",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_UnitOfMeasures_UnitOfMeasureId",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_UnitOfMeasureId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "DisplayQuantity",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasureId",
                table: "SaleItems");
        }
    }
}
