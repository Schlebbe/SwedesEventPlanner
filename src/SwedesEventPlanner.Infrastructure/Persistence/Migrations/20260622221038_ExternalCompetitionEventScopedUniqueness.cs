using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwedesEventPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalCompetitionEventScopedUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_external_competitions_event_id",
                schema: "public",
                table: "external_competitions");

            migrationBuilder.DropIndex(
                name: "IX_external_competitions_provider_external_id",
                schema: "public",
                table: "external_competitions");

            migrationBuilder.CreateIndex(
                name: "IX_external_competitions_event_id_provider_external_id",
                schema: "public",
                table: "external_competitions",
                columns: new[] { "event_id", "provider", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_external_competitions_event_id_provider_external_id",
                schema: "public",
                table: "external_competitions");

            migrationBuilder.CreateIndex(
                name: "IX_external_competitions_event_id",
                schema: "public",
                table: "external_competitions",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_competitions_provider_external_id",
                schema: "public",
                table: "external_competitions",
                columns: new[] { "provider", "external_id" },
                unique: true);
        }
    }
}
