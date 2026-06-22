using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SwedesEventPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalCompetitionMetricSyncContributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_progress_contributions_activity_events_activity_event~",
                schema: "public",
                table: "event_progress_contributions");

            migrationBuilder.DropForeignKey(
                name: "FK_event_progress_contributions_players_player_id",
                schema: "public",
                table: "event_progress_contributions");

            migrationBuilder.DropIndex(
                name: "IX_event_progress_contributions_event_id_tile_id_rule_id_activ~",
                schema: "public",
                table: "event_progress_contributions");

            migrationBuilder.AlterColumn<long>(
                name: "player_id",
                schema: "public",
                table: "event_progress_contributions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "activity_event_id",
                schema: "public",
                table: "event_progress_contributions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_event_progress_contributions_event_id_tile_id_rule_id_activ~",
                schema: "public",
                table: "event_progress_contributions",
                columns: new[] { "event_id", "tile_id", "rule_id", "activity_event_id" },
                unique: true,
                filter: "activity_event_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_event_progress_contributions_activity_events_activity_event~",
                schema: "public",
                table: "event_progress_contributions",
                column: "activity_event_id",
                principalSchema: "public",
                principalTable: "activity_events",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_event_progress_contributions_players_player_id",
                schema: "public",
                table: "event_progress_contributions",
                column: "player_id",
                principalSchema: "public",
                principalTable: "players",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_progress_contributions_activity_events_activity_event~",
                schema: "public",
                table: "event_progress_contributions");

            migrationBuilder.DropForeignKey(
                name: "FK_event_progress_contributions_players_player_id",
                schema: "public",
                table: "event_progress_contributions");

            migrationBuilder.DropIndex(
                name: "IX_event_progress_contributions_event_id_tile_id_rule_id_activ~",
                schema: "public",
                table: "event_progress_contributions");

            migrationBuilder.AlterColumn<long>(
                name: "player_id",
                schema: "public",
                table: "event_progress_contributions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "activity_event_id",
                schema: "public",
                table: "event_progress_contributions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_progress_contributions_event_id_tile_id_rule_id_activ~",
                schema: "public",
                table: "event_progress_contributions",
                columns: new[] { "event_id", "tile_id", "rule_id", "activity_event_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_event_progress_contributions_activity_events_activity_event~",
                schema: "public",
                table: "event_progress_contributions",
                column: "activity_event_id",
                principalSchema: "public",
                principalTable: "activity_events",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_event_progress_contributions_players_player_id",
                schema: "public",
                table: "event_progress_contributions",
                column: "player_id",
                principalSchema: "public",
                principalTable: "players",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
