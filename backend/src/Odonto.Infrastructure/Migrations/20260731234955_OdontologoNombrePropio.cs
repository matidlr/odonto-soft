using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OdontologoNombrePropio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Odontologos_Usuarios_UsuarioId",
                table: "Odontologos");

            migrationBuilder.AlterColumn<Guid>(
                name: "UsuarioId",
                table: "Odontologos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "Nombre",
                table: "Odontologos",
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "FK_Odontologos_Usuarios_UsuarioId",
                table: "Odontologos",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill: los odontólogos que ya existían tenían su nombre en
            // el Usuario asociado (login = odontólogo, 1 a 1). Ahora que
            // Nombre vive en Odontologo, copiamos ese valor una sola vez.
            migrationBuilder.Sql(@"
                UPDATE Odontologos o
                INNER JOIN Usuarios u ON o.UsuarioId = u.Id
                SET o.Nombre = u.Nombre
                WHERE o.Nombre = '' OR o.Nombre IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Odontologos_Usuarios_UsuarioId",
                table: "Odontologos");

            migrationBuilder.DropColumn(
                name: "Nombre",
                table: "Odontologos");

            migrationBuilder.AlterColumn<Guid>(
                name: "UsuarioId",
                table: "Odontologos",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddForeignKey(
                name: "FK_Odontologos_Usuarios_UsuarioId",
                table: "Odontologos",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
