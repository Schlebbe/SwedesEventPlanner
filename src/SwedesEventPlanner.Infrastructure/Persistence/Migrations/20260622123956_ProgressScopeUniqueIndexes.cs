using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwedesEventPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProgressScopeUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_tile_tier_progress_event_id_tile_tier_id_team_id_play~",
                schema: "public",
                table: "event_tile_tier_progress");

            migrationBuilder.DropIndex(
                name: "IX_event_tile_progress_event_id_tile_id_team_id_player_id",
                schema: "public",
                table: "event_tile_progress");

            migrationBuilder.CreateIndex(
                name: "ux_event_tile_tier_progress_event_scope",
                schema: "public",
                table: "event_tile_tier_progress",
                columns: new[] { "event_id", "tile_tier_id" },
                unique: true,
                filter: "team_id IS NULL AND player_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_event_tile_tier_progress_player_scope",
                schema: "public",
                table: "event_tile_tier_progress",
                columns: new[] { "event_id", "tile_tier_id", "player_id" },
                unique: true,
                filter: "team_id IS NULL AND player_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_event_tile_tier_progress_team_scope",
                schema: "public",
                table: "event_tile_tier_progress",
                columns: new[] { "event_id", "tile_tier_id", "team_id" },
                unique: true,
                filter: "team_id IS NOT NULL AND player_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_event_tile_progress_event_scope",
                schema: "public",
                table: "event_tile_progress",
                columns: new[] { "event_id", "tile_id" },
                unique: true,
                filter: "team_id IS NULL AND player_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_event_tile_progress_player_scope",
                schema: "public",
                table: "event_tile_progress",
                columns: new[] { "event_id", "tile_id", "player_id" },
                unique: true,
                filter: "team_id IS NULL AND player_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_event_tile_progress_team_scope",
                schema: "public",
                table: "event_tile_progress",
                columns: new[] { "event_id", "tile_id", "team_id" },
                unique: true,
                filter: "team_id IS NOT NULL AND player_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_event_tile_tier_progress_event_scope",
                schema: "public",
                table: "event_tile_tier_progress");

            migrationBuilder.DropIndex(
                name: "ux_event_tile_tier_progress_player_scope",
                schema: "public",
                table: "event_tile_tier_progress");

            migrationBuilder.DropIndex(
                name: "ux_event_tile_tier_progress_team_scope",
                schema: "public",
                table: "event_tile_tier_progress");

            migrationBuilder.DropIndex(
                name: "ux_event_tile_progress_event_scope",
                schema: "public",
                table: "event_tile_progress");

            migrationBuilder.DropIndex(
                name: "ux_event_tile_progress_player_scope",
                schema: "public",
                table: "event_tile_progress");

            migrationBuilder.DropIndex(
                name: "ux_event_tile_progress_team_scope",
                schema: "public",
                table: "event_tile_progress");

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_tier_progress_event_id_tile_tier_id_team_id_play~",
                schema: "public",
                table: "event_tile_tier_progress",
                columns: new[] { "event_id", "tile_tier_id", "team_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_progress_event_id_tile_id_team_id_player_id",
                schema: "public",
                table: "event_tile_progress",
                columns: new[] { "event_id", "tile_id", "team_id", "player_id" },
                unique: true);
        }
    }
}
