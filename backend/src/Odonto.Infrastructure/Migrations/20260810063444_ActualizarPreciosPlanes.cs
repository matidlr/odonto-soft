using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ActualizarPreciosPlanes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ajuste de precios pedido por Matias: Básico $25.000,
            // Profesional $45.000, Premium $60.000 (antes 15000/25000/35000).
            migrationBuilder.Sql(@"
                UPDATE Planes SET PrecioMensual = 25000 WHERE Id = '11111111-1111-1111-1111-111111111111';
                UPDATE Planes SET PrecioMensual = 45000 WHERE Id = '22222222-2222-2222-2222-222222222222';
                UPDATE Planes SET PrecioMensual = 60000 WHERE Id = '33333333-3333-3333-3333-333333333333';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE Planes SET PrecioMensual = 15000 WHERE Id = '11111111-1111-1111-1111-111111111111';
                UPDATE Planes SET PrecioMensual = 25000 WHERE Id = '22222222-2222-2222-2222-222222222222';
                UPDATE Planes SET PrecioMensual = 35000 WHERE Id = '33333333-3333-3333-3333-333333333333';
            ");
        }
    }
}
