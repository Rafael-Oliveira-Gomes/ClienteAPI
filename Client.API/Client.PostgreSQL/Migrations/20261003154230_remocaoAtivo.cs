using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Client.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class remocaoAtivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "Tatuadores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "Tatuadores",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
