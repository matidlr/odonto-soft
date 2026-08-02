using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotaEvolucionCamposEstructurados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Diagnostico",
                table: "NotasEvolucion",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Evolucion",
                table: "NotasEvolucion",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Medicacion",
                table: "NotasEvolucion",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Motivo",
                table: "NotasEvolucion",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Observaciones",
                table: "NotasEvolucion",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TratamientoRealizado",
                table: "NotasEvolucion",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Preservamos el contenido de las notas ya cargadas: pasa a
            // mostrarse en "Observaciones" antes de borrar la columna vieja.
            migrationBuilder.Sql(
                "UPDATE NotasEvolucion SET Observaciones = Contenido WHERE Contenido IS NOT NULL AND Contenido <> '';");

            migrationBuilder.DropColumn(
                name: "Contenido",
                table: "NotasEvolucion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Diagnostico",
                table: "NotasEvolucion");

            migrationBuilder.DropColumn(
                name: "Evolucion",
                table: "NotasEvolucion");

            migrationBuilder.DropColumn(
                name: "Medicacion",
                table: "NotasEvolucion");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "NotasEvolucion");

            migrationBuilder.DropColumn(
                name: "Observaciones",
                table: "NotasEvolucion");

            migrationBuilder.DropColumn(
                name: "TratamientoRealizado",
                table: "NotasEvolucion");

            migrationBuilder.AddColumn<string>(
                name: "Contenido",
                table: "NotasEvolucion",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
