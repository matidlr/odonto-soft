using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odonto.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResetPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TokensResetPassword",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Token = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Usado = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensResetPassword", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensResetPassword_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TokensResetPassword_Token",
                table: "TokensResetPassword",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensResetPassword_UsuarioId",
                table: "TokensResetPassword",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokensResetPassword");
        }
    }
}
