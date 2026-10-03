using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Client.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class tipotatuador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatusTatuador",
                table: "Tatuadores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipoTatuador",
                table: "Tatuadores",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusTatuador",
                table: "Tatuadores");

            migrationBuilder.DropColumn(
                name: "TipoTatuador",
                table: "Tatuadores");
        }
    }
}
