using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Agenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nombre",
                table: "Usuarios",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Disponibilidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OdontologoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TodoElDia = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    HoraFin = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    Bloqueado = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disponibilidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Disponibilidades_Odontologos_OdontologoId",
                        column: x => x.OdontologoId,
                        principalTable: "Odontologos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Disponibilidades_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TiposTratamiento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DuracionMinutos = table.Column<int>(type: "int", nullable: false),
                    PrecioBase = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposTratamiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TiposTratamiento_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Turnos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OdontologoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PacienteId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TipoTratamientoId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FechaHora = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DuracionMinutos = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Notas = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Turnos_Odontologos_OdontologoId",
                        column: x => x.OdontologoId,
                        principalTable: "Odontologos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Turnos_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Turnos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Turnos_TiposTratamiento_TipoTratamientoId",
                        column: x => x.TipoTratamientoId,
                        principalTable: "TiposTratamiento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TurnoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Canal = table.Column<int>(type: "int", nullable: false),
                    TipoRecordatorio = table.Column<int>(type: "int", nullable: false),
                    FechaEnvioProgramada = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Enviado = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FechaEnvioReal = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificaciones_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Disponibilidades_OdontologoId",
                table: "Disponibilidades",
                column: "OdontologoId");

            migrationBuilder.CreateIndex(
                name: "IX_Disponibilidades_TenantId",
                table: "Disponibilidades",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_Enviado_FechaEnvioProgramada",
                table: "Notificaciones",
                columns: new[] { "Enviado", "FechaEnvioProgramada" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_TurnoId",
                table: "Notificaciones",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_TiposTratamiento_TenantId",
                table: "TiposTratamiento",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_OdontologoId_FechaHora",
                table: "Turnos",
                columns: new[] { "OdontologoId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_PacienteId",
                table: "Turnos",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_TenantId",
                table: "Turnos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_TipoTratamientoId",
                table: "Turnos",
                column: "TipoTratamientoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Disponibilidades");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropTable(
                name: "Turnos");

            migrationBuilder.DropTable(
                name: "TiposTratamiento");

            migrationBuilder.DropColumn(
                name: "Nombre",
                table: "Usuarios");
        }
    }
}
