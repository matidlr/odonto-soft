using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArchivosOdontograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivosOdontograma",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EventoOdontogramaId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NombreOriginal = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RutaEnDisco = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TamanioBytes = table.Column<long>(type: "bigint", nullable: false),
                    FechaSubida = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosOdontograma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosOdontograma_EventosOdontograma_EventoOdontogramaId",
                        column: x => x.EventoOdontogramaId,
                        principalTable: "EventosOdontograma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchivosOdontograma_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosOdontograma_EventoOdontogramaId",
                table: "ArchivosOdontograma",
                column: "EventoOdontogramaId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosOdontograma_TenantId",
                table: "ArchivosOdontograma",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosOdontograma");
        }
    }
}
