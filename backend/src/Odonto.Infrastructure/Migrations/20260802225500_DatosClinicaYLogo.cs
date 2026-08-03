using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DatosClinicaYLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Tenants",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EmailContacto",
                table: "Tenants",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "Tenants",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LogoRutaEnDisco",
                table: "Tenants",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Tenants",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EmailContacto",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LogoRutaEnDisco",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Tenants");
        }
    }
}
