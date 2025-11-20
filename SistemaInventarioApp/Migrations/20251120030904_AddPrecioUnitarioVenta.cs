using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaInventarioApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPrecioUnitarioVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioUnitarioVenta",
                table: "Movimientos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropColumn(
                name: "PrecioUnitarioVenta",
                table: "Movimientos");
        }
    }
}
