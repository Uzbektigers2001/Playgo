using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playgo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewModerationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DislikesCount",
                table: "reviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "reviews",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DislikesCount",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "reviews");
        }
    }
}
