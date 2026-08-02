using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PruebaGratuita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFinPrueba",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TienePagoActivo",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // Las clínicas que ya existían (creadas antes del mes gratis)
            // arrancan su período de prueba de 30 días a partir de ahora,
            // para no cortarles el acceso de golpe.
            migrationBuilder.Sql(@"
                UPDATE Tenants
                SET FechaFinPrueba = DATE_ADD(UTC_TIMESTAMP(), INTERVAL 1 MONTH)
                WHERE FechaFinPrueba IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaFinPrueba",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TienePagoActivo",
                table: "Tenants");
        }
    }
}
