using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playgo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistAndPlaylists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "playlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CoverImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_playlists_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "watchlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watchlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_watchlists_contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "contents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_watchlists_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "playlist_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_playlist_items_contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "contents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_playlist_items_playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_playlist_items_ContentId",
                table: "playlist_items",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_playlist_items_PlaylistId_ContentId",
                table: "playlist_items",
                columns: new[] { "PlaylistId", "ContentId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_playlist_items_PlaylistId_OrderIndex",
                table: "playlist_items",
                columns: new[] { "PlaylistId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_playlists_IsPublic",
                table: "playlists",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_playlists_UserId",
                table: "playlists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_watchlists_ContentId",
                table: "watchlists",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_watchlists_UserId",
                table: "watchlists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_watchlists_UserId_ContentId",
                table: "watchlists",
                columns: new[] { "UserId", "ContentId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playlist_items");

            migrationBuilder.DropTable(
                name: "watchlists");

            migrationBuilder.DropTable(
                name: "playlists");
        }
    }
}
