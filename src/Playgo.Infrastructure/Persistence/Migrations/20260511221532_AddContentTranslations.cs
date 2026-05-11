using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playgo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_translations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Director = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Cast = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_translations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_translations_contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_translations_ContentId_LanguageCode",
                table: "content_translations",
                columns: new[] { "ContentId", "LanguageCode" },
                unique: true);

            // Backfill: create an "en" translation for every existing content using its current columns
            // as the default locale. Idempotent — skips rows that already have an "en" translation.
            migrationBuilder.Sql(@"
                INSERT INTO content_translations (
                    ""Id"", ""ContentId"", ""LanguageCode"",
                    ""Title"", ""OriginalTitle"", ""Description"", ""ShortDescription"",
                    ""Director"", ""Cast"",
                    ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"")
                SELECT
                    gen_random_uuid(), c.""Id"", 'en',
                    c.""Title"", c.""OriginalTitle"", c.""Description"", c.""ShortDescription"",
                    c.""Director"", c.""Cast"",
                    (now() at time zone 'utc'), NULL, false
                FROM contents c
                WHERE c.""IsDeleted"" = false
                  AND NOT EXISTS (
                    SELECT 1 FROM content_translations t
                    WHERE t.""ContentId"" = c.""Id"" AND t.""LanguageCode"" = 'en'
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_translations");
        }
    }
}
