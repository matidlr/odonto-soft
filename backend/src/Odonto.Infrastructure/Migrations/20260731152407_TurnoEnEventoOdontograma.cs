using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TurnoEnEventoOdontograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TurnoId",
                table: "EventosOdontograma",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_EventosOdontograma_TurnoId",
                table: "EventosOdontograma",
                column: "TurnoId");

            migrationBuilder.AddForeignKey(
                name: "FK_EventosOdontograma_Turnos_TurnoId",
                table: "EventosOdontograma",
                column: "TurnoId",
                principalTable: "Turnos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventosOdontograma_Turnos_TurnoId",
                table: "EventosOdontograma");

            migrationBuilder.DropIndex(
                name: "IX_EventosOdontograma_TurnoId",
                table: "EventosOdontograma");

            migrationBuilder.DropColumn(
                name: "TurnoId",
                table: "EventosOdontograma");
        }
    }
}
