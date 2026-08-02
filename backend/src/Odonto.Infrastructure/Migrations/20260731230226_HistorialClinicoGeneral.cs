using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HistorialClinicoGeneral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FichasMedicas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PacienteId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Alergias = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EnfermedadesPreexistentes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MedicacionActual = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Habitos = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Observaciones = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FichasMedicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FichasMedicas_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FichasMedicas_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NotasEvolucion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PacienteId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Contenido = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OdontologoId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TurnoId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Fecha = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasEvolucion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasEvolucion_Odontologos_OdontologoId",
                        column: x => x.OdontologoId,
                        principalTable: "Odontologos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NotasEvolucion_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotasEvolucion_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasEvolucion_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FichasMedicas_PacienteId",
                table: "FichasMedicas",
                column: "PacienteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FichasMedicas_TenantId",
                table: "FichasMedicas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasEvolucion_OdontologoId",
                table: "NotasEvolucion",
                column: "OdontologoId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasEvolucion_PacienteId_Fecha",
                table: "NotasEvolucion",
                columns: new[] { "PacienteId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasEvolucion_TenantId",
                table: "NotasEvolucion",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasEvolucion_TurnoId",
                table: "NotasEvolucion",
                column: "TurnoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FichasMedicas");

            migrationBuilder.DropTable(
                name: "NotasEvolucion");
        }
    }
}
