using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playgo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentQualityField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Quality",
                table: "contents",
                type: "integer",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quality",
                table: "contents");
        }
    }
}
