using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SwedesEventPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "events",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    slug = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    time_zone = table.Column<string>(type: "text", nullable: false, defaultValue: "Europe/Stockholm"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_groups",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    key = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "players",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    runescape_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bingo_boards",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    rows = table.Column<int>(type: "integer", nullable: true),
                    columns = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bingo_boards", x => x.id);
                    table.ForeignKey(
                        name: "FK_bingo_boards_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_teams",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_teams_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_competitions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    metric_type = table.Column<string>(type: "text", nullable: false),
                    metric_key = table.Column<string>(type: "text", nullable: false),
                    competition_mode = table.Column<string>(type: "text", nullable: false, defaultValue: "unknown"),
                    secret_reference = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "active"),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_successful_sync_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_public_sync_request_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_sync_status = table.Column<string>(type: "text", nullable: true),
                    last_sync_error = table.Column<string>(type: "text", nullable: true),
                    next_public_sync_available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_competitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_competitions_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_group_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    item_group_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    item_name = table.Column<string>(type: "text", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_group_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_group_items_item_groups_item_group_id",
                        column: x => x.item_group_id,
                        principalSchema: "public",
                        principalTable: "item_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_events",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    activity_type = table.Column<string>(type: "text", nullable: false),
                    source_system = table.Column<string>(type: "text", nullable: true),
                    source_endpoint = table.Column<string>(type: "text", nullable: true),
                    source_payload_version = table.Column<string>(type: "text", nullable: true),
                    account_profile_type = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    item_id = table.Column<int>(type: "integer", nullable: true),
                    item_name = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    skill = table.Column<string>(type: "text", nullable: true),
                    xp = table.Column<long>(type: "bigint", nullable: true),
                    boss_name = table.Column<string>(type: "text", nullable: true),
                    kc = table.Column<int>(type: "integer", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    raw_payload_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    dedupe_key = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_events_players_player_id",
                        column: x => x.player_id,
                        principalSchema: "public",
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_signups",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    player_id = table.Column<long>(type: "bigint", nullable: true),
                    runescape_name = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    email_hash = table.Column<string>(type: "text", nullable: true),
                    availability_text = table.Column<string>(type: "text", nullable: true),
                    daily_hours = table.Column<decimal>(type: "numeric", nullable: true),
                    preferred_content = table.Column<string>(type: "text", nullable: true),
                    team_preference = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "imported"),
                    source_system = table.Column<string>(type: "text", nullable: false, defaultValue: "google_forms"),
                    source_row_hash = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_signups", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_signups_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_signups_players_player_id",
                        column: x => x.player_id,
                        principalSchema: "public",
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "external_player_identities",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    provider = table.Column<string>(type: "text", nullable: false),
                    external_identifier = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    player_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "unmatched"),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<string>(type: "text", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_player_identities", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_player_identities_players_player_id",
                        column: x => x.player_id,
                        principalSchema: "public",
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "linked_accounts",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    external_identifier = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_linked_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_linked_accounts_players_player_id",
                        column: x => x.player_id,
                        principalSchema: "public",
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bingo_tiles",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    board_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    position_x = table.Column<int>(type: "integer", nullable: true),
                    position_y = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bingo_tiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_bingo_tiles_bingo_boards_board_id",
                        column: x => x.board_id,
                        principalSchema: "public",
                        principalTable: "bingo_boards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_participants",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "active"),
                    config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_participants_event_teams_team_id",
                        column: x => x.team_id,
                        principalSchema: "public",
                        principalTable: "event_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_event_participants_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_participants_players_player_id",
                        column: x => x.player_id,
                        principalSchema: "public",
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_competition_export_runs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    external_competition_id = table.Column<long>(type: "bigint", nullable: false),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    trigger_type = table.Column<string>(type: "text", nullable: false),
                    triggered_by = table.Column<string>(type: "text", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    participants_intended = table.Column<int>(type: "integer", nullable: true),
                    participants_added = table.Column<int>(type: "integer", nullable: true),
                    participants_removed = table.Column<int>(type: "integer", nullable: true),
                    team_mappings_intended = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    request_summary_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    response_summary_json = table.Column<string>(type: "jsonb", nullable: true),
                    validation_summary_json = table.Column<string>(type: "jsonb", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_competition_export_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_competition_export_runs_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_external_competition_export_runs_external_competitions_exte~",
                        column: x => x.external_competition_id,
                        principalSchema: "public",
                        principalTable: "external_competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_competition_sync_runs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    external_competition_id = table.Column<long>(type: "bigint", nullable: false),
                    trigger_type = table.Column<string>(type: "text", nullable: false),
                    triggered_by = table.Column<string>(type: "text", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    rows_read = table.Column<int>(type: "integer", nullable: true),
                    rows_changed = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    raw_response_json = table.Column<string>(type: "jsonb", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_competition_sync_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_competition_sync_runs_external_competitions_extern~",
                        column: x => x.external_competition_id,
                        principalSchema: "public",
                        principalTable: "external_competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_competition_team_metrics",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    external_competition_id = table.Column<long>(type: "bigint", nullable: false),
                    local_team_id = table.Column<long>(type: "bigint", nullable: true),
                    temple_team_key = table.Column<string>(type: "text", nullable: false),
                    team_name = table.Column<string>(type: "text", nullable: false),
                    metric_type = table.Column<string>(type: "text", nullable: false),
                    metric_key = table.Column<string>(type: "text", nullable: false),
                    start_value = table.Column<long>(type: "bigint", nullable: true),
                    current_value = table.Column<long>(type: "bigint", nullable: true),
                    gained_value = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    rank = table.Column<int>(type: "integer", nullable: true),
                    mvp_runescape_name = table.Column<string>(type: "text", nullable: true),
                    members_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_competition_team_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_competition_team_metrics_event_teams_local_team_id",
                        column: x => x.local_team_id,
                        principalSchema: "public",
                        principalTable: "event_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_external_competition_team_metrics_external_competitions_ext~",
                        column: x => x.external_competition_id,
                        principalSchema: "public",
                        principalTable: "external_competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_event_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    activity_event_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    item_name = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    source = table.Column<string>(type: "text", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_event_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_event_items_activity_events_activity_event_id",
                        column: x => x.activity_event_id,
                        principalSchema: "public",
                        principalTable: "activity_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_event_metrics",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    activity_event_id = table.Column<long>(type: "bigint", nullable: false),
                    metric_type = table.Column<string>(type: "text", nullable: false),
                    metric_key = table.Column<string>(type: "text", nullable: false),
                    metric_value = table.Column<long>(type: "bigint", nullable: true),
                    metric_bool = table.Column<bool>(type: "boolean", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_event_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_event_metrics_activity_events_activity_event_id",
                        column: x => x.activity_event_id,
                        principalSchema: "public",
                        principalTable: "activity_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_processing_queue",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    activity_event_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_processing_queue", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_processing_queue_activity_events_activity_event_id",
                        column: x => x.activity_event_id,
                        principalSchema: "public",
                        principalTable: "activity_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_competition_player_reviews",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    external_competition_id = table.Column<long>(type: "bigint", nullable: false),
                    external_player_identity_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "unreviewed"),
                    ignored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<string>(type: "text", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_competition_player_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_competition_player_reviews_external_competitions_e~",
                        column: x => x.external_competition_id,
                        principalSchema: "public",
                        principalTable: "external_competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_external_competition_player_reviews_external_player_identit~",
                        column: x => x.external_player_identity_id,
                        principalSchema: "public",
                        principalTable: "external_player_identities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bingo_tile_tiers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    tile_id = table.Column<long>(type: "bigint", nullable: false),
                    tier_number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    score_value = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_required_for_board_completion = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bingo_tile_tiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_bingo_tile_tiers_bingo_tiles_tile_id",
                        column: x => x.tile_id,
                        principalSchema: "public",
                        principalTable: "bingo_tiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_tile_progress",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    tile_id = table.Column<long>(type: "bigint", nullable: false),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    player_id = table.Column<long>(type: "bigint", nullable: true),
                    current_value = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 0m),
                    current_tier = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_tile_progress", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_tile_progress_bingo_tiles_tile_id",
                        column: x => x.tile_id,
                        principalSchema: "public",
                        principalTable: "bingo_tiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_tile_progress_event_teams_team_id",
                        column: x => x.team_id,
                        principalSchema: "public",
                        principalTable: "event_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_tile_progress_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_tile_progress_players_player_id",
                        column: x => x.player_id,
                        principalSchema: "public",
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tile_unlock_conditions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    tile_id = table.Column<long>(type: "bigint", nullable: false),
                    condition_type = table.Column<string>(type: "text", nullable: false),
                    config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tile_unlock_conditions", x => x.id);
                    table.ForeignKey(
                        name: "FK_tile_unlock_conditions_bingo_tiles_tile_id",
                        column: x => x.tile_id,
                        principalSchema: "public",
                        principalTable: "bingo_tiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_competition_metrics",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    external_competition_id = table.Column<long>(type: "bigint", nullable: false),
                    external_player_identity_id = table.Column<long>(type: "bigint", nullable: true),
                    external_competition_player_review_id = table.Column<long>(type: "bigint", nullable: true),
                    player_id = table.Column<long>(type: "bigint", nullable: true),
                    runescape_name = table.Column<string>(type: "text", nullable: false),
                    metric_type = table.Column<string>(type: "text", nullable: false),
                    metric_key = table.Column<string>(type: "text", nullable: false),
                    start_value = table.Column<long>(type: "bigint", nullable: true),
                    current_value = table.Column<long>(type: "bigint", nullable: true),
                    gained_value = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    rank = table.Column<int>(type: "integer", nullable: true),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_competition_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_competition_metrics_external_competition_player_re~",
                        column: x => x.external_competition_player_review_id,
                        principalSchema: "public",
                        principalTable: "external_competition_player_reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_external_competition_metrics_external_competitions_external~",
                        column: x => x.external_competition_id,
                        principalSchema: "public",
                        principalTable: "external_competitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_external_competition_metrics_external_player_identities_ext~",
                        column: x => x.external_player_identity_id,
                        principalSchema: "public",
                        principalTable: "external_player_identities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_external_competition_metrics_players_player_id",
                        column: x => x.player_id,
                        principalSchema: "public",
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "event_tile_tier_progress",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    tile_id = table.Column<long>(type: "bigint", nullable: false),
                    tile_tier_id = table.Column<long>(type: "bigint", nullable: false),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    player_id = table.Column<long>(type: "bigint", nullable: true),
                    current_value = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 0m),
                    is_achieved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    achieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_scored = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    scored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    score_awarded = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_tile_tier_progress", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_tile_tier_progress_bingo_tile_tiers_tile_tier_id",
                        column: x => x.tile_tier_id,
                        principalSchema: "public",
                        principalTable: "bingo_tile_tiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_tile_tier_progress_bingo_tiles_tile_id",
                        column: x => x.tile_id,
                        principalSchema: "public",
                        principalTable: "bingo_tiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_tile_tier_progress_event_teams_team_id",
                        column: x => x.team_id,
                        principalSchema: "public",
                        principalTable: "event_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_tile_tier_progress_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_tile_tier_progress_players_player_id",
                        column: x => x.player_id,
                        principalSchema: "public",
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tile_rules",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    tile_id = table.Column<long>(type: "bigint", nullable: false),
                    tile_tier_id = table.Column<long>(type: "bigint", nullable: true),
                    rule_type = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tile_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_tile_rules_bingo_tile_tiers_tile_tier_id",
                        column: x => x.tile_tier_id,
                        principalSchema: "public",
                        principalTable: "bingo_tile_tiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tile_rules_bingo_tiles_tile_id",
                        column: x => x.tile_id,
                        principalSchema: "public",
                        principalTable: "bingo_tiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_progress_contributions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    tile_id = table.Column<long>(type: "bigint", nullable: false),
                    tile_tier_id = table.Column<long>(type: "bigint", nullable: true),
                    rule_id = table.Column<long>(type: "bigint", nullable: false),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    activity_event_id = table.Column<long>(type: "bigint", nullable: false),
                    value_added = table.Column<decimal>(type: "numeric", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_progress_contributions", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_progress_contributions_activity_events_activity_event~",
                        column: x => x.activity_event_id,
                        principalSchema: "public",
                        principalTable: "activity_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_progress_contributions_bingo_tile_tiers_tile_tier_id",
                        column: x => x.tile_tier_id,
                        principalSchema: "public",
                        principalTable: "bingo_tile_tiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_event_progress_contributions_bingo_tiles_tile_id",
                        column: x => x.tile_id,
                        principalSchema: "public",
                        principalTable: "bingo_tiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_progress_contributions_event_teams_team_id",
                        column: x => x.team_id,
                        principalSchema: "public",
                        principalTable: "event_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_event_progress_contributions_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_progress_contributions_players_player_id",
                        column: x => x.player_id,
                        principalSchema: "public",
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_progress_contributions_tile_rules_rule_id",
                        column: x => x.rule_id,
                        principalSchema: "public",
                        principalTable: "tile_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_activity_event_items_activity",
                schema: "public",
                table: "activity_event_items",
                column: "activity_event_id");

            migrationBuilder.CreateIndex(
                name: "idx_activity_event_items_item",
                schema: "public",
                table: "activity_event_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_event_metrics_activity_event_id",
                schema: "public",
                table: "activity_event_metrics",
                column: "activity_event_id");

            migrationBuilder.CreateIndex(
                name: "idx_activity_events_dedupe_key",
                schema: "public",
                table: "activity_events",
                column: "dedupe_key",
                unique: true,
                filter: "dedupe_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_activity_events_player_time",
                schema: "public",
                table: "activity_events",
                columns: new[] { "player_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "idx_activity_events_type_time",
                schema: "public",
                table: "activity_events",
                columns: new[] { "activity_type", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "idx_activity_processing_queue_pending",
                schema: "public",
                table: "activity_processing_queue",
                columns: new[] { "status", "available_at" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_processing_queue_activity_event_id",
                schema: "public",
                table: "activity_processing_queue",
                column: "activity_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_bingo_boards_event_id",
                schema: "public",
                table: "bingo_boards",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "IX_bingo_tile_tiers_tile_id_tier_number",
                schema: "public",
                table: "bingo_tile_tiers",
                columns: new[] { "tile_id", "tier_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bingo_tiles_board_id",
                schema: "public",
                table: "bingo_tiles",
                column: "board_id");

            migrationBuilder.CreateIndex(
                name: "idx_event_participants_player",
                schema: "public",
                table: "event_participants",
                columns: new[] { "player_id", "event_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_event_id_player_id",
                schema: "public",
                table: "event_participants",
                columns: new[] { "event_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_team_id",
                schema: "public",
                table: "event_participants",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_progress_contributions_activity_event_id",
                schema: "public",
                table: "event_progress_contributions",
                column: "activity_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_progress_contributions_event_id_tile_id_rule_id_activ~",
                schema: "public",
                table: "event_progress_contributions",
                columns: new[] { "event_id", "tile_id", "rule_id", "activity_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_progress_contributions_player_id",
                schema: "public",
                table: "event_progress_contributions",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_progress_contributions_rule_id",
                schema: "public",
                table: "event_progress_contributions",
                column: "rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_progress_contributions_team_id",
                schema: "public",
                table: "event_progress_contributions",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_progress_contributions_tile_id",
                schema: "public",
                table: "event_progress_contributions",
                column: "tile_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_progress_contributions_tile_tier_id",
                schema: "public",
                table: "event_progress_contributions",
                column: "tile_tier_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_signups_event_id_source_system_source_row_hash",
                schema: "public",
                table: "event_signups",
                columns: new[] { "event_id", "source_system", "source_row_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_signups_player_id",
                schema: "public",
                table: "event_signups",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_teams_event_id_name",
                schema: "public",
                table: "event_teams",
                columns: new[] { "event_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_progress_event_id_tile_id_team_id_player_id",
                schema: "public",
                table: "event_tile_progress",
                columns: new[] { "event_id", "tile_id", "team_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_progress_player_id",
                schema: "public",
                table: "event_tile_progress",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_progress_team_id",
                schema: "public",
                table: "event_tile_progress",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_progress_tile_id",
                schema: "public",
                table: "event_tile_progress",
                column: "tile_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_tier_progress_event_id_tile_tier_id_team_id_play~",
                schema: "public",
                table: "event_tile_tier_progress",
                columns: new[] { "event_id", "tile_tier_id", "team_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_tier_progress_player_id",
                schema: "public",
                table: "event_tile_tier_progress",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_tier_progress_team_id",
                schema: "public",
                table: "event_tile_tier_progress",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_tier_progress_tile_id",
                schema: "public",
                table: "event_tile_tier_progress",
                column: "tile_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_tile_tier_progress_tile_tier_id",
                schema: "public",
                table: "event_tile_tier_progress",
                column: "tile_tier_id");

            migrationBuilder.CreateIndex(
                name: "idx_events_active_window",
                schema: "public",
                table: "events",
                columns: new[] { "status", "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "IX_events_slug",
                schema: "public",
                table: "events",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_competition_export_runs_event_id",
                schema: "public",
                table: "external_competition_export_runs",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_competition_export_runs_external_competition_id",
                schema: "public",
                table: "external_competition_export_runs",
                column: "external_competition_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_competition_metrics_external_competition_id_runesc~",
                schema: "public",
                table: "external_competition_metrics",
                columns: new[] { "external_competition_id", "runescape_name", "metric_type", "metric_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_competition_metrics_external_competition_player_re~",
                schema: "public",
                table: "external_competition_metrics",
                column: "external_competition_player_review_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_competition_metrics_external_player_identity_id",
                schema: "public",
                table: "external_competition_metrics",
                column: "external_player_identity_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_competition_metrics_player_id",
                schema: "public",
                table: "external_competition_metrics",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_competition_player_reviews_external_competition_id~",
                schema: "public",
                table: "external_competition_player_reviews",
                columns: new[] { "external_competition_id", "external_player_identity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_competition_player_reviews_external_player_identit~",
                schema: "public",
                table: "external_competition_player_reviews",
                column: "external_player_identity_id");

            migrationBuilder.CreateIndex(
                name: "idx_external_competition_sync_runs_one_active",
                schema: "public",
                table: "external_competition_sync_runs",
                column: "external_competition_id",
                unique: true,
                filter: "status IN ('queued', 'running')");

            migrationBuilder.CreateIndex(
                name: "IX_external_competition_team_metrics_external_competition_id_t~",
                schema: "public",
                table: "external_competition_team_metrics",
                columns: new[] { "external_competition_id", "temple_team_key", "metric_type", "metric_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_competition_team_metrics_local_team_id",
                schema: "public",
                table: "external_competition_team_metrics",
                column: "local_team_id");

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

            migrationBuilder.CreateIndex(
                name: "IX_external_player_identities_player_id",
                schema: "public",
                table: "external_player_identities",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_player_identities_provider_external_identifier",
                schema: "public",
                table: "external_player_identities",
                columns: new[] { "provider", "external_identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_group_items_item_group_id_item_id",
                schema: "public",
                table: "item_group_items",
                columns: new[] { "item_group_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_groups_key",
                schema: "public",
                table: "item_groups",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_linked_accounts_player_id",
                schema: "public",
                table: "linked_accounts",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_linked_accounts_provider_external_identifier",
                schema: "public",
                table: "linked_accounts",
                columns: new[] { "provider", "external_identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_players_runescape_name",
                schema: "public",
                table: "players",
                column: "runescape_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tile_rules_tile_id",
                schema: "public",
                table: "tile_rules",
                column: "tile_id");

            migrationBuilder.CreateIndex(
                name: "IX_tile_rules_tile_tier_id",
                schema: "public",
                table: "tile_rules",
                column: "tile_tier_id");

            migrationBuilder.CreateIndex(
                name: "IX_tile_unlock_conditions_tile_id",
                schema: "public",
                table: "tile_unlock_conditions",
                column: "tile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_event_items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "activity_event_metrics",
                schema: "public");

            migrationBuilder.DropTable(
                name: "activity_processing_queue",
                schema: "public");

            migrationBuilder.DropTable(
                name: "event_participants",
                schema: "public");

            migrationBuilder.DropTable(
                name: "event_progress_contributions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "event_signups",
                schema: "public");

            migrationBuilder.DropTable(
                name: "event_tile_progress",
                schema: "public");

            migrationBuilder.DropTable(
                name: "event_tile_tier_progress",
                schema: "public");

            migrationBuilder.DropTable(
                name: "external_competition_export_runs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "external_competition_metrics",
                schema: "public");

            migrationBuilder.DropTable(
                name: "external_competition_sync_runs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "external_competition_team_metrics",
                schema: "public");

            migrationBuilder.DropTable(
                name: "item_group_items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "linked_accounts",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tile_unlock_conditions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "activity_events",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tile_rules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "external_competition_player_reviews",
                schema: "public");

            migrationBuilder.DropTable(
                name: "event_teams",
                schema: "public");

            migrationBuilder.DropTable(
                name: "item_groups",
                schema: "public");

            migrationBuilder.DropTable(
                name: "bingo_tile_tiers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "external_competitions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "external_player_identities",
                schema: "public");

            migrationBuilder.DropTable(
                name: "bingo_tiles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "players",
                schema: "public");

            migrationBuilder.DropTable(
                name: "bingo_boards",
                schema: "public");

            migrationBuilder.DropTable(
                name: "events",
                schema: "public");
        }
    }
}
