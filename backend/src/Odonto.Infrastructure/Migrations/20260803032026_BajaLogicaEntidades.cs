using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BajaLogicaEntidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Presupuestos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Presupuestos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Presupuestos",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Disponibilidades",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Disponibilidades",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Disponibilidades",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Consentimientos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Consentimientos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Consentimientos",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Cobros",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Cobros",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Cobros",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ArchivosPaciente",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "ArchivosPaciente",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ArchivosPaciente",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Presupuestos");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Presupuestos");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Presupuestos");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Disponibilidades");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Disponibilidades");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Disponibilidades");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Consentimientos");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Consentimientos");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Consentimientos");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Cobros");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Cobros");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Cobros");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ArchivosPaciente");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ArchivosPaciente");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ArchivosPaciente");
        }
    }
}
