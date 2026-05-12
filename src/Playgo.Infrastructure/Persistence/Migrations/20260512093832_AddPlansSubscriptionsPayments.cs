using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playgo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlansSubscriptionsPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FeaturesJson = table.Column<string>(type: "text", nullable: true),
                    MaxQuality = table.Column<int>(type: "integer", nullable: false),
                    MaxConcurrentStreams = table.Column<int>(type: "integer", nullable: false),
                    HasAds = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsDownload = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "plans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_subscriptions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RawResponseJson = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "plans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payments_user_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "user_subscriptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_payments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_PlanId",
                table: "payments",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_ProviderTransactionId",
                table: "payments",
                column: "ProviderTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_SubscriptionId",
                table: "payments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_UserId",
                table: "payments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_plans_Code",
                table: "plans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_PlanId",
                table: "user_subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_UserId_Status",
                table: "user_subscriptions",
                columns: new[] { "UserId", "Status" });

            var freeId = new Guid("11111111-1111-1111-1111-111111111111");
            var monthlyId = new Guid("22222222-2222-2222-2222-222222222222");
            var yearlyId = new Guid("33333333-3333-3333-3333-333333333333");
            var seededAt = new DateTime(2026, 5, 12, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "plans",
                columns: new[] { "Id", "Code", "Name", "Description", "Price", "Currency", "Period", "IsActive", "FeaturesJson", "MaxQuality", "MaxConcurrentStreams", "HasAds", "AllowsDownload", "CreatedAt", "UpdatedAt", "IsDeleted" },
                values: new object[,]
                {
                    { freeId, "free", "Free", "Free plan with ads and limited quality.", 0m, "UZS", 1, true, "[\"HD 720p\",\"Reklama bilan\",\"1 ekran\"]", 720, 1, true, false, seededAt, null, false },
                    { monthlyId, "premium_monthly", "Premium Monthly", "Premium content, 4K, no ads, downloads.", 49000m, "UZS", 1, true, "[\"4K Ultra HD\",\"Reklamasiz\",\"2 ekran\",\"Offline yuklab olish\"]", 2160, 2, false, true, seededAt, null, false },
                    { yearlyId, "premium_yearly", "Premium Yearly", "All Premium features, billed yearly.", 490000m, "UZS", 3, true, "[\"4K Ultra HD\",\"Reklamasiz\",\"4 ekran\",\"Offline yuklab olish\",\"Yillik chegirma\"]", 2160, 4, false, true, seededAt, null, false }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "user_subscriptions");

            migrationBuilder.DropTable(
                name: "plans");
        }
    }
}
