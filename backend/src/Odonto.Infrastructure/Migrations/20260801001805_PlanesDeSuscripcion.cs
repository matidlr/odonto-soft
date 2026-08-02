using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlanesDeSuscripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Planes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MaxOdontologos = table.Column<int>(type: "int", nullable: false),
                    PrecioMensual = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Activo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Planes de referencia. Los precios son un placeholder — se
            // pueden ajustar después a mano (tabla Planes) o desde un panel
            // de SuperAdmin más adelante.
            migrationBuilder.InsertData(
                table: "Planes",
                columns: new[] { "Id", "Nombre", "MaxOdontologos", "PrecioMensual", "Activo", "Orden" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "Básico", 1, 15000m, true, 1 },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "Profesional", 2, 25000m, true, 2 },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "Premium", 3, 35000m, true, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_PlanId",
                table: "Tenants",
                column: "PlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Planes_PlanId",
                table: "Tenants",
                column: "PlanId",
                principalTable: "Planes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Las clínicas que ya existían no tenían plan asignado: las
            // dejamos en el Básico (1 odontólogo) por defecto.
            migrationBuilder.Sql(@"
                UPDATE Tenants
                SET PlanId = '11111111-1111-1111-1111-111111111111'
                WHERE PlanId IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Planes_PlanId",
                table: "Tenants");

            migrationBuilder.DropTable(
                name: "Planes");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_PlanId",
                table: "Tenants");
        }
    }
}
