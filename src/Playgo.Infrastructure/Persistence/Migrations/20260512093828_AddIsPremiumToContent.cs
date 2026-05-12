using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playgo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPremiumToContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPremium",
                table: "contents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPremium",
                table: "contents");
        }
    }
}
