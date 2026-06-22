using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Domain.Activity;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.ExternalCompetitions;
using SwedesEventPlanner.Domain.Items;
using SwedesEventPlanner.Domain.Players;

namespace SwedesEventPlanner.Infrastructure.Persistence;

public sealed class EventPlannerDbContext(DbContextOptions<EventPlannerDbContext> options)
    : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<LinkedAccount> LinkedAccounts => Set<LinkedAccount>();
    public DbSet<ExternalPlayerIdentity> ExternalPlayerIdentities => Set<ExternalPlayerIdentity>();

    public DbSet<EventDefinition> Events => Set<EventDefinition>();
    public DbSet<EventTeam> EventTeams => Set<EventTeam>();
    public DbSet<EventSignup> EventSignups => Set<EventSignup>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();

    public DbSet<ExternalCompetition> ExternalCompetitions => Set<ExternalCompetition>();
    public DbSet<ExternalCompetitionExportRun> ExternalCompetitionExportRuns => Set<ExternalCompetitionExportRun>();
    public DbSet<ExternalCompetitionPlayerReview> ExternalCompetitionPlayerReviews => Set<ExternalCompetitionPlayerReview>();
    public DbSet<ExternalCompetitionSyncRun> ExternalCompetitionSyncRuns => Set<ExternalCompetitionSyncRun>();
    public DbSet<ExternalCompetitionMetric> ExternalCompetitionMetrics => Set<ExternalCompetitionMetric>();
    public DbSet<ExternalCompetitionTeamMetric> ExternalCompetitionTeamMetrics => Set<ExternalCompetitionTeamMetric>();

    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();
    public DbSet<ActivityEventItem> ActivityEventItems => Set<ActivityEventItem>();
    public DbSet<ActivityEventMetric> ActivityEventMetrics => Set<ActivityEventMetric>();
    public DbSet<ActivityProcessingQueueItem> ActivityProcessingQueue => Set<ActivityProcessingQueueItem>();

    public DbSet<BingoBoard> BingoBoards => Set<BingoBoard>();
    public DbSet<BingoTile> BingoTiles => Set<BingoTile>();
    public DbSet<BingoTileTier> BingoTileTiers => Set<BingoTileTier>();
    public DbSet<TileRule> TileRules => Set<TileRule>();
    public DbSet<TileUnlockCondition> TileUnlockConditions => Set<TileUnlockCondition>();
    public DbSet<EventTileProgress> EventTileProgress => Set<EventTileProgress>();
    public DbSet<EventTileTierProgress> EventTileTierProgress => Set<EventTileTierProgress>();
    public DbSet<EventProgressContribution> EventProgressContributions => Set<EventProgressContribution>();

    public DbSet<ItemGroup> ItemGroups => Set<ItemGroup>();
    public DbSet<ItemGroupItem> ItemGroupItems => Set<ItemGroupItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.UseSerialColumns();

        MapPlayers(modelBuilder);
        MapEvents(modelBuilder);
        MapExternalCompetitions(modelBuilder);
        MapActivity(modelBuilder);
        MapBingo(modelBuilder);
        MapItems(modelBuilder);
    }

    private static void MapPlayers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(player => player.Id);

            entity.Property(player => player.Id).HasColumnName("id");
            entity.Property(player => player.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(player => player.RuneScapeName).HasColumnName("runescape_name").IsRequired();
            entity.Property(player => player.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");

            entity.HasIndex(player => player.RuneScapeName).IsUnique();
        });

        modelBuilder.Entity<LinkedAccount>(entity =>
        {
            entity.ToTable("linked_accounts");
            entity.HasKey(account => account.Id);

            entity.Property(account => account.Id).HasColumnName("id");
            entity.Property(account => account.PlayerId).HasColumnName("player_id");
            entity.Property(account => account.Provider).HasColumnName("provider").IsRequired();
            entity.Property(account => account.ExternalIdentifier).HasColumnName("external_identifier").IsRequired();
            entity.Property(account => account.DisplayName).HasColumnName("display_name");
            entity.Property(account => account.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");
            MapJsonObject(entity.Property(account => account.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(account => account.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(account => new { account.Provider, account.ExternalIdentifier }).IsUnique();
        });

        modelBuilder.Entity<ExternalPlayerIdentity>(entity =>
        {
            entity.ToTable("external_player_identities");
            entity.HasKey(identity => identity.Id);

            entity.Property(identity => identity.Id).HasColumnName("id");
            entity.Property(identity => identity.Provider).HasColumnName("provider").IsRequired();
            entity.Property(identity => identity.ExternalIdentifier).HasColumnName("external_identifier").IsRequired();
            entity.Property(identity => identity.DisplayName).HasColumnName("display_name").IsRequired();
            entity.Property(identity => identity.PlayerId).HasColumnName("player_id");
            entity.Property(identity => identity.Status)
                .HasColumnName("status")
                .HasDefaultValue(ExternalPlayerIdentityStatuses.Unmatched)
                .IsRequired();
            entity.Property(identity => identity.FirstSeenAt)
                .HasColumnName("first_seen_at")
                .HasDefaultValueSql("now()");
            entity.Property(identity => identity.LastSeenAt)
                .HasColumnName("last_seen_at")
                .HasDefaultValueSql("now()");
            entity.Property(identity => identity.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(identity => identity.ReviewedBy).HasColumnName("reviewed_by");
            MapJsonObject(entity.Property(identity => identity.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(identity => identity.PlayerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(identity => new { identity.Provider, identity.ExternalIdentifier }).IsUnique();
        });
    }

    private static void MapEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventDefinition>(entity =>
        {
            entity.ToTable("events");
            entity.HasKey(eventDefinition => eventDefinition.Id);

            entity.Property(eventDefinition => eventDefinition.Id).HasColumnName("id");
            entity.Property(eventDefinition => eventDefinition.Slug).HasColumnName("slug").IsRequired();
            entity.Property(eventDefinition => eventDefinition.Name).HasColumnName("name").IsRequired();
            entity.Property(eventDefinition => eventDefinition.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(eventDefinition => eventDefinition.Status).HasColumnName("status").IsRequired();
            entity.Property(eventDefinition => eventDefinition.StartsAt).HasColumnName("starts_at");
            entity.Property(eventDefinition => eventDefinition.EndsAt).HasColumnName("ends_at");
            entity.Property(eventDefinition => eventDefinition.TimeZone)
                .HasColumnName("time_zone")
                .HasDefaultValue(EventDefaults.TimeZone)
                .IsRequired();
            entity.Property(eventDefinition => eventDefinition.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");
            MapJsonObject(entity.Property(eventDefinition => eventDefinition.ConfigJson).HasColumnName("config_json"));

            entity.HasIndex(eventDefinition => eventDefinition.Slug).IsUnique();
            entity.HasIndex(eventDefinition => new
            {
                eventDefinition.Status,
                eventDefinition.StartsAt,
                eventDefinition.EndsAt,
            }).HasDatabaseName("idx_events_active_window");
        });

        modelBuilder.Entity<EventTeam>(entity =>
        {
            entity.ToTable("event_teams");
            entity.HasKey(team => team.Id);

            entity.Property(team => team.Id).HasColumnName("id");
            entity.Property(team => team.EventId).HasColumnName("event_id");
            entity.Property(team => team.Name).HasColumnName("name").IsRequired();
            entity.Property(team => team.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            MapJsonObject(entity.Property(team => team.ConfigJson).HasColumnName("config_json"));

            entity.HasOne<EventDefinition>()
                .WithMany()
                .HasForeignKey(team => team.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(team => new { team.EventId, team.Name }).IsUnique();
        });

        modelBuilder.Entity<EventSignup>(entity =>
        {
            entity.ToTable("event_signups");
            entity.HasKey(signup => signup.Id);

            entity.Property(signup => signup.Id).HasColumnName("id");
            entity.Property(signup => signup.EventId).HasColumnName("event_id");
            entity.Property(signup => signup.PlayerId).HasColumnName("player_id");
            entity.Property(signup => signup.RuneScapeName).HasColumnName("runescape_name").IsRequired();
            entity.Property(signup => signup.DisplayName).HasColumnName("display_name");
            entity.Property(signup => signup.EmailHash).HasColumnName("email_hash");
            entity.Property(signup => signup.AvailabilityText).HasColumnName("availability_text");
            entity.Property(signup => signup.DailyHours).HasColumnName("daily_hours").HasColumnType("numeric");
            entity.Property(signup => signup.PreferredContent).HasColumnName("preferred_content");
            entity.Property(signup => signup.TeamPreference).HasColumnName("team_preference");
            entity.Property(signup => signup.Notes).HasColumnName("notes");
            entity.Property(signup => signup.Status)
                .HasColumnName("status")
                .HasDefaultValue(EventSignupStatuses.Imported)
                .IsRequired();
            entity.Property(signup => signup.SourceSystem)
                .HasColumnName("source_system")
                .HasDefaultValue(EventSignupSources.GoogleForms)
                .IsRequired();
            entity.Property(signup => signup.SourceRowHash).HasColumnName("source_row_hash");
            entity.Property(signup => signup.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(signup => signup.ImportedAt).HasColumnName("imported_at").HasDefaultValueSql("now()");
            MapJsonObject(entity.Property(signup => signup.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<EventDefinition>().WithMany().HasForeignKey(signup => signup.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Player>().WithMany().HasForeignKey(signup => signup.PlayerId).OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(signup => new { signup.EventId, signup.SourceSystem, signup.SourceRowHash }).IsUnique();
        });

        modelBuilder.Entity<EventParticipant>(entity =>
        {
            entity.ToTable("event_participants");
            entity.HasKey(participant => participant.Id);

            entity.Property(participant => participant.Id).HasColumnName("id");
            entity.Property(participant => participant.EventId).HasColumnName("event_id");
            entity.Property(participant => participant.PlayerId).HasColumnName("player_id");
            entity.Property(participant => participant.TeamId).HasColumnName("team_id");
            entity.Property(participant => participant.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()");
            entity.Property(participant => participant.Status)
                .HasColumnName("status")
                .HasDefaultValue(EventParticipantStatuses.Active)
                .IsRequired();
            MapJsonObject(entity.Property(participant => participant.ConfigJson).HasColumnName("config_json"));

            entity.HasOne<EventDefinition>().WithMany().HasForeignKey(participant => participant.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Player>().WithMany().HasForeignKey(participant => participant.PlayerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<EventTeam>().WithMany().HasForeignKey(participant => participant.TeamId).OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(participant => new { participant.EventId, participant.PlayerId }).IsUnique();
            entity.HasIndex(participant => new { participant.PlayerId, participant.EventId, participant.Status })
                .HasDatabaseName("idx_event_participants_player");
        });
    }

    private static void MapExternalCompetitions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExternalCompetition>(entity =>
        {
            entity.ToTable("external_competitions");
            entity.HasKey(competition => competition.Id);

            entity.Property(competition => competition.Id).HasColumnName("id");
            entity.Property(competition => competition.EventId).HasColumnName("event_id");
            entity.Property(competition => competition.Provider).HasColumnName("provider").IsRequired();
            entity.Property(competition => competition.ExternalId).HasColumnName("external_id").IsRequired();
            entity.Property(competition => competition.Name).HasColumnName("name").IsRequired();
            entity.Property(competition => competition.MetricType).HasColumnName("metric_type").IsRequired();
            entity.Property(competition => competition.MetricKey).HasColumnName("metric_key").IsRequired();
            entity.Property(competition => competition.CompetitionMode)
                .HasColumnName("competition_mode")
                .HasDefaultValue(ExternalCompetitionModes.Unknown)
                .IsRequired();
            entity.Property(competition => competition.SecretReference).HasColumnName("secret_reference");
            entity.Property(competition => competition.Status)
                .HasColumnName("status")
                .HasDefaultValue(ExternalCompetitionStatuses.Active)
                .IsRequired();
            entity.Property(competition => competition.LastSyncedAt).HasColumnName("last_synced_at");
            entity.Property(competition => competition.LastSuccessfulSyncAt).HasColumnName("last_successful_sync_at");
            entity.Property(competition => competition.LastPublicSyncRequestAcceptedAt).HasColumnName("last_public_sync_request_accepted_at");
            entity.Property(competition => competition.LastSyncStatus).HasColumnName("last_sync_status");
            entity.Property(competition => competition.LastSyncError).HasColumnName("last_sync_error");
            entity.Property(competition => competition.NextPublicSyncAvailableAt).HasColumnName("next_public_sync_available_at");
            entity.Property(competition => competition.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            MapJsonObject(entity.Property(competition => competition.ConfigJson).HasColumnName("config_json"));

            entity.HasOne<EventDefinition>().WithMany().HasForeignKey(competition => competition.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(competition => new { competition.Provider, competition.ExternalId }).IsUnique();
        });

        modelBuilder.Entity<ExternalCompetitionExportRun>(entity =>
        {
            entity.ToTable("external_competition_export_runs");
            entity.HasKey(run => run.Id);

            entity.Property(run => run.Id).HasColumnName("id");
            entity.Property(run => run.ExternalCompetitionId).HasColumnName("external_competition_id");
            entity.Property(run => run.EventId).HasColumnName("event_id");
            entity.Property(run => run.TriggerType).HasColumnName("trigger_type").IsRequired();
            entity.Property(run => run.TriggeredBy).HasColumnName("triggered_by");
            entity.Property(run => run.RequestedAt).HasColumnName("requested_at").HasDefaultValueSql("now()");
            entity.Property(run => run.StartedAt).HasColumnName("started_at");
            entity.Property(run => run.CompletedAt).HasColumnName("completed_at");
            entity.Property(run => run.Status).HasColumnName("status").IsRequired();
            entity.Property(run => run.ParticipantsIntended).HasColumnName("participants_intended");
            entity.Property(run => run.ParticipantsAdded).HasColumnName("participants_added");
            entity.Property(run => run.ParticipantsRemoved).HasColumnName("participants_removed");
            entity.Property(run => run.TeamMappingsIntended).HasColumnName("team_mappings_intended");
            entity.Property(run => run.ErrorMessage).HasColumnName("error_message");
            MapJsonObject(entity.Property(run => run.RequestSummaryJson).HasColumnName("request_summary_json"));
            MapNullableJsonObject(entity.Property(run => run.ResponseSummaryJson).HasColumnName("response_summary_json"));
            MapNullableJsonObject(entity.Property(run => run.ValidationSummaryJson).HasColumnName("validation_summary_json"));
            MapJsonObject(entity.Property(run => run.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<ExternalCompetition>().WithMany().HasForeignKey(run => run.ExternalCompetitionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<EventDefinition>().WithMany().HasForeignKey(run => run.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExternalCompetitionPlayerReview>(entity =>
        {
            entity.ToTable("external_competition_player_reviews");
            entity.HasKey(review => review.Id);

            entity.Property(review => review.Id).HasColumnName("id");
            entity.Property(review => review.ExternalCompetitionId).HasColumnName("external_competition_id");
            entity.Property(review => review.ExternalPlayerIdentityId).HasColumnName("external_player_identity_id");
            entity.Property(review => review.Status)
                .HasColumnName("status")
                .HasDefaultValue(ExternalCompetitionPlayerReviewStatuses.Unreviewed)
                .IsRequired();
            entity.Property(review => review.IgnoredAt).HasColumnName("ignored_at");
            entity.Property(review => review.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(review => review.ReviewedBy).HasColumnName("reviewed_by");
            MapJsonObject(entity.Property(review => review.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<ExternalCompetition>().WithMany().HasForeignKey(review => review.ExternalCompetitionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ExternalPlayerIdentity>().WithMany().HasForeignKey(review => review.ExternalPlayerIdentityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(review => new { review.ExternalCompetitionId, review.ExternalPlayerIdentityId }).IsUnique();
        });

        modelBuilder.Entity<ExternalCompetitionSyncRun>(entity =>
        {
            entity.ToTable("external_competition_sync_runs");
            entity.HasKey(run => run.Id);

            entity.Property(run => run.Id).HasColumnName("id");
            entity.Property(run => run.ExternalCompetitionId).HasColumnName("external_competition_id");
            entity.Property(run => run.TriggerType).HasColumnName("trigger_type").IsRequired();
            entity.Property(run => run.TriggeredBy).HasColumnName("triggered_by");
            entity.Property(run => run.RequestedAt).HasColumnName("requested_at");
            entity.Property(run => run.StartedAt).HasColumnName("started_at").HasDefaultValueSql("now()");
            entity.Property(run => run.CompletedAt).HasColumnName("completed_at");
            entity.Property(run => run.Status).HasColumnName("status").IsRequired();
            entity.Property(run => run.RowsRead).HasColumnName("rows_read");
            entity.Property(run => run.RowsChanged).HasColumnName("rows_changed");
            entity.Property(run => run.ErrorMessage).HasColumnName("error_message");
            MapNullableJsonObject(entity.Property(run => run.RawResponseJson).HasColumnName("raw_response_json"));
            MapJsonObject(entity.Property(run => run.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<ExternalCompetition>().WithMany().HasForeignKey(run => run.ExternalCompetitionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(run => run.ExternalCompetitionId)
                .HasDatabaseName("idx_external_competition_sync_runs_one_active")
                .IsUnique()
                .HasFilter("status IN ('queued', 'running')");
        });

        modelBuilder.Entity<ExternalCompetitionMetric>(entity =>
        {
            entity.ToTable("external_competition_metrics");
            entity.HasKey(metric => metric.Id);

            entity.Property(metric => metric.Id).HasColumnName("id");
            entity.Property(metric => metric.ExternalCompetitionId).HasColumnName("external_competition_id");
            entity.Property(metric => metric.ExternalPlayerIdentityId).HasColumnName("external_player_identity_id");
            entity.Property(metric => metric.ExternalCompetitionPlayerReviewId).HasColumnName("external_competition_player_review_id");
            entity.Property(metric => metric.PlayerId).HasColumnName("player_id");
            entity.Property(metric => metric.RuneScapeName).HasColumnName("runescape_name").IsRequired();
            entity.Property(metric => metric.MetricType).HasColumnName("metric_type").IsRequired();
            entity.Property(metric => metric.MetricKey).HasColumnName("metric_key").IsRequired();
            entity.Property(metric => metric.StartValue).HasColumnName("start_value");
            entity.Property(metric => metric.CurrentValue).HasColumnName("current_value");
            entity.Property(metric => metric.GainedValue).HasColumnName("gained_value").HasDefaultValue(0L);
            entity.Property(metric => metric.Rank).HasColumnName("rank");
            entity.Property(metric => metric.LastSyncedAt).HasColumnName("last_synced_at");
            MapJsonObject(entity.Property(metric => metric.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<ExternalCompetition>().WithMany().HasForeignKey(metric => metric.ExternalCompetitionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ExternalPlayerIdentity>().WithMany().HasForeignKey(metric => metric.ExternalPlayerIdentityId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<ExternalCompetitionPlayerReview>().WithMany().HasForeignKey(metric => metric.ExternalCompetitionPlayerReviewId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Player>().WithMany().HasForeignKey(metric => metric.PlayerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(metric => new { metric.ExternalCompetitionId, metric.RuneScapeName, metric.MetricType, metric.MetricKey }).IsUnique();
        });

        modelBuilder.Entity<ExternalCompetitionTeamMetric>(entity =>
        {
            entity.ToTable("external_competition_team_metrics");
            entity.HasKey(metric => metric.Id);

            entity.Property(metric => metric.Id).HasColumnName("id");
            entity.Property(metric => metric.ExternalCompetitionId).HasColumnName("external_competition_id");
            entity.Property(metric => metric.LocalTeamId).HasColumnName("local_team_id");
            entity.Property(metric => metric.TempleTeamKey).HasColumnName("temple_team_key").IsRequired();
            entity.Property(metric => metric.TeamName).HasColumnName("team_name").IsRequired();
            entity.Property(metric => metric.MetricType).HasColumnName("metric_type").IsRequired();
            entity.Property(metric => metric.MetricKey).HasColumnName("metric_key").IsRequired();
            entity.Property(metric => metric.StartValue).HasColumnName("start_value");
            entity.Property(metric => metric.CurrentValue).HasColumnName("current_value");
            entity.Property(metric => metric.GainedValue).HasColumnName("gained_value").HasDefaultValue(0L);
            entity.Property(metric => metric.Rank).HasColumnName("rank");
            entity.Property(metric => metric.MvpRuneScapeName).HasColumnName("mvp_runescape_name");
            MapJsonArray(entity.Property(metric => metric.MembersJson).HasColumnName("members_json"));
            entity.Property(metric => metric.LastSyncedAt).HasColumnName("last_synced_at");
            MapJsonObject(entity.Property(metric => metric.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<ExternalCompetition>().WithMany().HasForeignKey(metric => metric.ExternalCompetitionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<EventTeam>().WithMany().HasForeignKey(metric => metric.LocalTeamId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(metric => new { metric.ExternalCompetitionId, metric.TempleTeamKey, metric.MetricType, metric.MetricKey }).IsUnique();
        });
    }

    private static void MapActivity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityEvent>(entity =>
        {
            entity.ToTable("activity_events");
            entity.HasKey(activity => activity.Id);

            entity.Property(activity => activity.Id).HasColumnName("id");
            entity.Property(activity => activity.PlayerId).HasColumnName("player_id");
            entity.Property(activity => activity.ActivityType).HasColumnName("activity_type").IsRequired();
            entity.Property(activity => activity.SourceSystem).HasColumnName("source_system");
            entity.Property(activity => activity.SourceEndpoint).HasColumnName("source_endpoint");
            entity.Property(activity => activity.SourcePayloadVersion).HasColumnName("source_payload_version");
            entity.Property(activity => activity.AccountProfileType).HasColumnName("account_profile_type");
            entity.Property(activity => activity.Source).HasColumnName("source");
            entity.Property(activity => activity.ItemId).HasColumnName("item_id");
            entity.Property(activity => activity.ItemName).HasColumnName("item_name");
            entity.Property(activity => activity.Quantity).HasColumnName("quantity");
            entity.Property(activity => activity.Skill).HasColumnName("skill");
            entity.Property(activity => activity.Xp).HasColumnName("xp");
            entity.Property(activity => activity.BossName).HasColumnName("boss_name");
            entity.Property(activity => activity.Kc).HasColumnName("kc");
            entity.Property(activity => activity.OccurredAt).HasColumnName("occurred_at");
            entity.Property(activity => activity.ReceivedAt).HasColumnName("received_at").HasDefaultValueSql("now()");
            MapJsonObject(entity.Property(activity => activity.RawPayloadJson).HasColumnName("raw_payload_json"));
            entity.Property(activity => activity.DedupeKey).HasColumnName("dedupe_key");

            entity.HasOne<Player>().WithMany().HasForeignKey(activity => activity.PlayerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(activity => new { activity.PlayerId, activity.OccurredAt }).HasDatabaseName("idx_activity_events_player_time");
            entity.HasIndex(activity => new { activity.ActivityType, activity.OccurredAt }).HasDatabaseName("idx_activity_events_type_time");
            entity.HasIndex(activity => activity.DedupeKey)
                .HasDatabaseName("idx_activity_events_dedupe_key")
                .IsUnique()
                .HasFilter("dedupe_key IS NOT NULL");
        });

        modelBuilder.Entity<ActivityEventItem>(entity =>
        {
            entity.ToTable("activity_event_items");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.ActivityEventId).HasColumnName("activity_event_id");
            entity.Property(item => item.ItemId).HasColumnName("item_id");
            entity.Property(item => item.ItemName).HasColumnName("item_name");
            entity.Property(item => item.Quantity).HasColumnName("quantity").HasDefaultValue(1);
            entity.Property(item => item.Source).HasColumnName("source");
            MapJsonObject(entity.Property(item => item.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<ActivityEvent>().WithMany().HasForeignKey(item => item.ActivityEventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.ItemId).HasDatabaseName("idx_activity_event_items_item");
            entity.HasIndex(item => item.ActivityEventId).HasDatabaseName("idx_activity_event_items_activity");
        });

        modelBuilder.Entity<ActivityEventMetric>(entity =>
        {
            entity.ToTable("activity_event_metrics");
            entity.HasKey(metric => metric.Id);

            entity.Property(metric => metric.Id).HasColumnName("id");
            entity.Property(metric => metric.ActivityEventId).HasColumnName("activity_event_id");
            entity.Property(metric => metric.MetricType).HasColumnName("metric_type").IsRequired();
            entity.Property(metric => metric.MetricKey).HasColumnName("metric_key").IsRequired();
            entity.Property(metric => metric.MetricValue).HasColumnName("metric_value");
            entity.Property(metric => metric.MetricBool).HasColumnName("metric_bool");
            MapJsonObject(entity.Property(metric => metric.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<ActivityEvent>().WithMany().HasForeignKey(metric => metric.ActivityEventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityProcessingQueueItem>(entity =>
        {
            entity.ToTable("activity_processing_queue");
            entity.HasKey(queueItem => queueItem.Id);

            entity.Property(queueItem => queueItem.Id).HasColumnName("id");
            entity.Property(queueItem => queueItem.ActivityEventId).HasColumnName("activity_event_id");
            entity.Property(queueItem => queueItem.Status)
                .HasColumnName("status")
                .HasDefaultValue(ActivityProcessingStatuses.Pending)
                .IsRequired();
            entity.Property(queueItem => queueItem.Attempts).HasColumnName("attempts").HasDefaultValue(0);
            entity.Property(queueItem => queueItem.AvailableAt).HasColumnName("available_at").HasDefaultValueSql("now()");
            entity.Property(queueItem => queueItem.LockedAt).HasColumnName("locked_at");
            entity.Property(queueItem => queueItem.ProcessedAt).HasColumnName("processed_at");
            entity.Property(queueItem => queueItem.ErrorMessage).HasColumnName("error_message");
            entity.Property(queueItem => queueItem.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasOne<ActivityEvent>().WithMany().HasForeignKey(queueItem => queueItem.ActivityEventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(queueItem => new { queueItem.Status, queueItem.AvailableAt })
                .HasDatabaseName("idx_activity_processing_queue_pending");
        });
    }

    private static void MapBingo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BingoBoard>(entity =>
        {
            entity.ToTable("bingo_boards");
            entity.HasKey(board => board.Id);

            entity.Property(board => board.Id).HasColumnName("id");
            entity.Property(board => board.EventId).HasColumnName("event_id");
            entity.Property(board => board.Name).HasColumnName("name").IsRequired();
            entity.Property(board => board.Rows).HasColumnName("rows");
            entity.Property(board => board.Columns).HasColumnName("columns");
            entity.Property(board => board.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            MapJsonObject(entity.Property(board => board.ConfigJson).HasColumnName("config_json"));

            entity.HasOne<EventDefinition>().WithMany().HasForeignKey(board => board.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BingoTile>(entity =>
        {
            entity.ToTable("bingo_tiles");
            entity.HasKey(tile => tile.Id);

            entity.Property(tile => tile.Id).HasColumnName("id");
            entity.Property(tile => tile.BoardId).HasColumnName("board_id");
            entity.Property(tile => tile.Title).HasColumnName("title").IsRequired();
            entity.Property(tile => tile.Description).HasColumnName("description");
            entity.Property(tile => tile.PositionX).HasColumnName("position_x");
            entity.Property(tile => tile.PositionY).HasColumnName("position_y");
            entity.Property(tile => tile.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            MapJsonObject(entity.Property(tile => tile.ConfigJson).HasColumnName("config_json"));

            entity.HasOne<BingoBoard>().WithMany().HasForeignKey(tile => tile.BoardId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BingoTileTier>(entity =>
        {
            entity.ToTable("bingo_tile_tiers");
            entity.HasKey(tier => tier.Id);

            entity.Property(tier => tier.Id).HasColumnName("id");
            entity.Property(tier => tier.TileId).HasColumnName("tile_id");
            entity.Property(tier => tier.TierNumber).HasColumnName("tier_number");
            entity.Property(tier => tier.Title).HasColumnName("title");
            entity.Property(tier => tier.Description).HasColumnName("description");
            entity.Property(tier => tier.ScoreValue).HasColumnName("score_value").HasDefaultValue(1);
            entity.Property(tier => tier.IsRequiredForBoardCompletion)
                .HasColumnName("is_required_for_board_completion")
                .HasDefaultValue(true);
            entity.Property(tier => tier.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            MapJsonObject(entity.Property(tier => tier.ConfigJson).HasColumnName("config_json"));

            entity.HasOne<BingoTile>().WithMany().HasForeignKey(tier => tier.TileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(tier => new { tier.TileId, tier.TierNumber }).IsUnique();
        });

        modelBuilder.Entity<TileRule>(entity =>
        {
            entity.ToTable("tile_rules");
            entity.HasKey(rule => rule.Id);

            entity.Property(rule => rule.Id).HasColumnName("id");
            entity.Property(rule => rule.TileId).HasColumnName("tile_id");
            entity.Property(rule => rule.TileTierId).HasColumnName("tile_tier_id");
            entity.Property(rule => rule.RuleType).HasColumnName("rule_type").IsRequired();
            entity.Property(rule => rule.Scope).HasColumnName("scope").IsRequired();
            entity.Property(rule => rule.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            MapJsonObject(entity.Property(rule => rule.ConfigJson).HasColumnName("config_json"));
            entity.Property(rule => rule.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasOne<BingoTile>().WithMany().HasForeignKey(rule => rule.TileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BingoTileTier>().WithMany().HasForeignKey(rule => rule.TileTierId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TileUnlockCondition>(entity =>
        {
            entity.ToTable("tile_unlock_conditions");
            entity.HasKey(condition => condition.Id);

            entity.Property(condition => condition.Id).HasColumnName("id");
            entity.Property(condition => condition.TileId).HasColumnName("tile_id");
            entity.Property(condition => condition.ConditionType).HasColumnName("condition_type").IsRequired();
            MapJsonObject(entity.Property(condition => condition.ConfigJson).HasColumnName("config_json"));
            entity.Property(condition => condition.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasOne<BingoTile>().WithMany().HasForeignKey(condition => condition.TileId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventTileProgress>(entity =>
        {
            entity.ToTable("event_tile_progress");
            entity.HasKey(progress => progress.Id);

            entity.Property(progress => progress.Id).HasColumnName("id");
            entity.Property(progress => progress.EventId).HasColumnName("event_id");
            entity.Property(progress => progress.TileId).HasColumnName("tile_id");
            entity.Property(progress => progress.TeamId).HasColumnName("team_id");
            entity.Property(progress => progress.PlayerId).HasColumnName("player_id");
            entity.Property(progress => progress.CurrentValue).HasColumnName("current_value").HasColumnType("numeric").HasDefaultValue(0m);
            entity.Property(progress => progress.CurrentTier).HasColumnName("current_tier").HasDefaultValue(0);
            entity.Property(progress => progress.IsCompleted).HasColumnName("is_completed").HasDefaultValue(false);
            entity.Property(progress => progress.CompletedAt).HasColumnName("completed_at");
            entity.Property(progress => progress.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            MapJsonObject(entity.Property(progress => progress.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<EventDefinition>().WithMany().HasForeignKey(progress => progress.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BingoTile>().WithMany().HasForeignKey(progress => progress.TileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<EventTeam>().WithMany().HasForeignKey(progress => progress.TeamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Player>().WithMany().HasForeignKey(progress => progress.PlayerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(progress => new { progress.EventId, progress.TileId, progress.TeamId, progress.PlayerId }).IsUnique();
        });

        modelBuilder.Entity<EventTileTierProgress>(entity =>
        {
            entity.ToTable("event_tile_tier_progress");
            entity.HasKey(progress => progress.Id);

            entity.Property(progress => progress.Id).HasColumnName("id");
            entity.Property(progress => progress.EventId).HasColumnName("event_id");
            entity.Property(progress => progress.TileId).HasColumnName("tile_id");
            entity.Property(progress => progress.TileTierId).HasColumnName("tile_tier_id");
            entity.Property(progress => progress.TeamId).HasColumnName("team_id");
            entity.Property(progress => progress.PlayerId).HasColumnName("player_id");
            entity.Property(progress => progress.CurrentValue).HasColumnName("current_value").HasColumnType("numeric").HasDefaultValue(0m);
            entity.Property(progress => progress.IsAchieved).HasColumnName("is_achieved").HasDefaultValue(false);
            entity.Property(progress => progress.AchievedAt).HasColumnName("achieved_at");
            entity.Property(progress => progress.IsScored).HasColumnName("is_scored").HasDefaultValue(false);
            entity.Property(progress => progress.ScoredAt).HasColumnName("scored_at");
            entity.Property(progress => progress.ScoreAwarded).HasColumnName("score_awarded").HasDefaultValue(0);
            entity.Property(progress => progress.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            MapJsonObject(entity.Property(progress => progress.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<EventDefinition>().WithMany().HasForeignKey(progress => progress.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BingoTile>().WithMany().HasForeignKey(progress => progress.TileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BingoTileTier>().WithMany().HasForeignKey(progress => progress.TileTierId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<EventTeam>().WithMany().HasForeignKey(progress => progress.TeamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Player>().WithMany().HasForeignKey(progress => progress.PlayerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(progress => new { progress.EventId, progress.TileTierId, progress.TeamId, progress.PlayerId }).IsUnique();
        });

        modelBuilder.Entity<EventProgressContribution>(entity =>
        {
            entity.ToTable("event_progress_contributions");
            entity.HasKey(contribution => contribution.Id);

            entity.Property(contribution => contribution.Id).HasColumnName("id");
            entity.Property(contribution => contribution.EventId).HasColumnName("event_id");
            entity.Property(contribution => contribution.TileId).HasColumnName("tile_id");
            entity.Property(contribution => contribution.TileTierId).HasColumnName("tile_tier_id");
            entity.Property(contribution => contribution.RuleId).HasColumnName("rule_id");
            entity.Property(contribution => contribution.TeamId).HasColumnName("team_id");
            entity.Property(contribution => contribution.PlayerId).HasColumnName("player_id");
            entity.Property(contribution => contribution.ActivityEventId).HasColumnName("activity_event_id");
            entity.Property(contribution => contribution.ValueAdded).HasColumnName("value_added").HasColumnType("numeric");
            entity.Property(contribution => contribution.Description).HasColumnName("description");
            entity.Property(contribution => contribution.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            MapJsonObject(entity.Property(contribution => contribution.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<EventDefinition>().WithMany().HasForeignKey(contribution => contribution.EventId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BingoTile>().WithMany().HasForeignKey(contribution => contribution.TileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BingoTileTier>().WithMany().HasForeignKey(contribution => contribution.TileTierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<TileRule>().WithMany().HasForeignKey(contribution => contribution.RuleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<EventTeam>().WithMany().HasForeignKey(contribution => contribution.TeamId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Player>().WithMany().HasForeignKey(contribution => contribution.PlayerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<ActivityEvent>().WithMany().HasForeignKey(contribution => contribution.ActivityEventId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(contribution => new { contribution.EventId, contribution.TileId, contribution.RuleId, contribution.ActivityEventId })
                .IsUnique()
                .HasFilter("activity_event_id IS NOT NULL");
        });
    }

    private static void MapItems(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ItemGroup>(entity =>
        {
            entity.ToTable("item_groups");
            entity.HasKey(group => group.Id);

            entity.Property(group => group.Id).HasColumnName("id");
            entity.Property(group => group.Key).HasColumnName("key").IsRequired();
            entity.Property(group => group.Name).HasColumnName("name").IsRequired();
            entity.Property(group => group.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasIndex(group => group.Key).IsUnique();
        });

        modelBuilder.Entity<ItemGroupItem>(entity =>
        {
            entity.ToTable("item_group_items");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.ItemGroupId).HasColumnName("item_group_id");
            entity.Property(item => item.ItemId).HasColumnName("item_id");
            entity.Property(item => item.ItemName).HasColumnName("item_name").IsRequired();
            MapJsonObject(entity.Property(item => item.MetadataJson).HasColumnName("metadata_json"));

            entity.HasOne<ItemGroup>().WithMany().HasForeignKey(item => item.ItemGroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.ItemGroupId, item.ItemId }).IsUnique();
        });
    }

    private static Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<string> MapJsonObject(
        Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<string> propertyBuilder)
    {
        return propertyBuilder
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();
    }

    private static Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<string> MapJsonArray(
        Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<string> propertyBuilder)
    {
        return propertyBuilder
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired();
    }

    private static Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<string?> MapNullableJsonObject(
        Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<string?> propertyBuilder)
    {
        return propertyBuilder.HasColumnType("jsonb");
    }
}
