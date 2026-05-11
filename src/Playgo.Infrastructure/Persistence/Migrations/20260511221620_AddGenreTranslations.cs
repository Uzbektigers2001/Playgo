using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playgo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGenreTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "genre_translations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GenreId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genre_translations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_genre_translations_genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_genre_translations_GenreId_LanguageCode",
                table: "genre_translations",
                columns: new[] { "GenreId", "LanguageCode" },
                unique: true);

            // Backfill: create an "en" translation for every existing genre using its current columns
            // as the default locale. Idempotent — skips rows that already have an "en" translation.
            migrationBuilder.Sql(@"
                INSERT INTO genre_translations (
                    ""Id"", ""GenreId"", ""LanguageCode"",
                    ""Name"", ""Description"",
                    ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"")
                SELECT
                    gen_random_uuid(), g.""Id"", 'en',
                    g.""Name"", g.""Description"",
                    (now() at time zone 'utc'), NULL, false
                FROM genres g
                WHERE g.""IsDeleted"" = false
                  AND NOT EXISTS (
                    SELECT 1 FROM genre_translations t
                    WHERE t.""GenreId"" = g.""Id"" AND t.""LanguageCode"" = 'en'
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "genre_translations");
        }
    }
}
