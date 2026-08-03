using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sedes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SedeId",
                table: "Turnos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SedeId",
                table: "Disponibilidades",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "Sedes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OdontologoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Direccion = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EsPrincipal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Activa = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sedes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sedes_Odontologos_OdontologoId",
                        column: x => x.OdontologoId,
                        principalTable: "Odontologos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sedes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_SedeId",
                table: "Turnos",
                column: "SedeId");

            migrationBuilder.CreateIndex(
                name: "IX_Disponibilidades_SedeId",
                table: "Disponibilidades",
                column: "SedeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sedes_OdontologoId",
                table: "Sedes",
                column: "OdontologoId");

            migrationBuilder.CreateIndex(
                name: "IX_Sedes_TenantId",
                table: "Sedes",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Disponibilidades_Sedes_SedeId",
                table: "Disponibilidades",
                column: "SedeId",
                principalTable: "Sedes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Turnos_Sedes_SedeId",
                table: "Turnos",
                column: "SedeId",
                principalTable: "Sedes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Crea una sede "Principal" para cada odontólogo ya existente,
            // y hace que sus turnos y disponibilidades ya cargados apunten a ella.
            migrationBuilder.Sql(@"
                INSERT INTO Sedes (Id, TenantId, OdontologoId, Nombre, Direccion, EsPrincipal, Activa)
                SELECT UUID(), TenantId, Id, 'Principal', NULL, 1, 1 FROM Odontologos;
            ");

            migrationBuilder.Sql(@"
                UPDATE Disponibilidades d
                JOIN Sedes s ON s.OdontologoId = d.OdontologoId AND s.EsPrincipal = 1
                SET d.SedeId = s.Id
                WHERE d.SedeId IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE Turnos t
                JOIN Sedes s ON s.OdontologoId = t.OdontologoId AND s.EsPrincipal = 1
                SET t.SedeId = s.Id
                WHERE t.SedeId IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Disponibilidades_Sedes_SedeId",
                table: "Disponibilidades");

            migrationBuilder.DropForeignKey(
                name: "FK_Turnos_Sedes_SedeId",
                table: "Turnos");

            migrationBuilder.DropTable(
                name: "Sedes");

            migrationBuilder.DropIndex(
                name: "IX_Turnos_SedeId",
                table: "Turnos");

            migrationBuilder.DropIndex(
                name: "IX_Disponibilidades_SedeId",
                table: "Disponibilidades");

            migrationBuilder.DropColumn(
                name: "SedeId",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "SedeId",
                table: "Disponibilidades");
        }
    }
}
