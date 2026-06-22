using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SwedesEventPlanner.Application.Clock;
using SwedesEventPlanner.Contracts.Activity;
using SwedesEventPlanner.Domain.Activity;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Common;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.Players;
using SwedesEventPlanner.Infrastructure.Activity;
using SwedesEventPlanner.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SwedesEventPlanner.Application.Tests.Persistence;

public sealed class PostgreSqlIntegrationTests(PostgreSqlIntegrationFixture fixture)
    : IClassFixture<PostgreSqlIntegrationFixture>
{
    private static readonly DateTimeOffset TestNow = new(2026, 7, 2, 18, 30, 0, TimeSpan.Zero);

    [PostgreSqlIntegrationFact]
    public async Task Migrations_apply_to_empty_postgresql_database()
    {
        await using var dbContext = await fixture.CreateMigratedDbContextAsync();

        var migrationIds = await dbContext.Database
            .SqlQueryRaw<string>("SELECT migration_id AS \"Value\" FROM public.\"__EFMigrationsHistory\" ORDER BY migration_id")
            .ToListAsync();
        var progressIndexes = await dbContext.Database
            .SqlQueryRaw<string>("SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND tablename IN ('event_tile_progress', 'event_tile_tier_progress') ORDER BY indexname")
            .ToListAsync();

        Assert.Contains(migrationIds, id => id.EndsWith("_ProgressScopeUniqueIndexes", StringComparison.Ordinal));
        Assert.Contains("ux_event_tile_progress_team_scope", progressIndexes);
        Assert.Contains("ux_event_tile_tier_progress_team_scope", progressIndexes);
    }

    [PostgreSqlIntegrationFact]
    public async Task Progress_scope_unique_indexes_reject_duplicate_team_progress()
    {
        await using var dbContext = await fixture.CreateMigratedDbContextAsync();
        var seed = await SeedProgressScopeAsync(dbContext);

        dbContext.EventTileProgress.AddRange(
            CreateTileProgress(seed.EventId, seed.TileId, seed.TeamId),
            CreateTileProgress(seed.EventId, seed.TileId, seed.TeamId));
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        dbContext.EventTileTierProgress.AddRange(
            CreateTierProgress(seed.EventId, seed.TileId, seed.TierId, seed.TeamId),
            CreateTierProgress(seed.EventId, seed.TileId, seed.TierId, seed.TeamId));
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [PostgreSqlIntegrationFact]
    public async Task Activity_queue_claiming_uses_skip_locked_without_duplicate_claims()
    {
        await using var dbContext = await fixture.CreateMigratedDbContextAsync();
        var player = await SeedPlayerAsync(dbContext);
        await AddQueuedActivityAsync(dbContext, player.Id, "queue-claim-1");
        await AddQueuedActivityAsync(dbContext, player.Id, "queue-claim-2");

        await using var workerOneContext = fixture.CreateDbContext(dbContext.Database.GetConnectionString()!);
        await using var workerTwoContext = fixture.CreateDbContext(dbContext.Database.GetConnectionString()!);
        var workerOne = CreateProcessingService(workerOneContext);
        var workerTwo = CreateProcessingService(workerTwoContext);

        var results = await Task.WhenAll(
            workerOne.ProcessPendingActivityAsync(1, CancellationToken.None),
            workerTwo.ProcessPendingActivityAsync(1, CancellationToken.None));

        Assert.Equal([1, 1], results.Order().ToArray());
        Assert.Equal(2, await dbContext.ActivityProcessingQueue.CountAsync(queue => queue.Status == ActivityProcessingStatuses.Processed));
        Assert.Equal(0, await dbContext.ActivityProcessingQueue.CountAsync(queue => queue.Status == ActivityProcessingStatuses.Pending));
    }

    [PostgreSqlIntegrationFact]
    public async Task Activity_ingestion_dedupe_returns_existing_activity_and_does_not_queue_duplicate()
    {
        await using var dbContext = await fixture.CreateMigratedDbContextAsync();
        await SeedPlayerAsync(dbContext);
        var service = new ActivityIngestionService(dbContext, new FixedClock(TestNow));
        var request = new CreateActivityRequest
        {
            PlayerName = "Sebbe",
            ActivityType = ActivityTypes.ItemDrop,
            Source = "Abyssal demons",
            ItemId = 4151,
            ItemName = "Abyssal whip",
            Quantity = 1,
            OccurredAt = TestNow,
            DedupeKey = "postgres-dedupe"
        };

        var first = await service.CreateActivityAsync(request, CancellationToken.None);
        var second = await service.CreateActivityAsync(request, CancellationToken.None);

        Assert.False(first.Duplicate);
        Assert.True(second.Duplicate);
        Assert.Equal(first.ActivityEventId, second.ActivityEventId);
        Assert.Equal(1, await dbContext.ActivityEvents.CountAsync());
        Assert.Equal(1, await dbContext.ActivityProcessingQueue.CountAsync());
    }

    private static ActivityProcessingService CreateProcessingService(EventPlannerDbContext dbContext)
    {
        return new ActivityProcessingService(
            dbContext,
            new FixedClock(TestNow),
            NullLogger<ActivityProcessingService>.Instance);
    }

    private static async Task<Player> SeedPlayerAsync(EventPlannerDbContext dbContext)
    {
        var player = new Player
        {
            DisplayName = "Sebbe",
            RuneScapeName = "Sebbe",
            CreatedAt = TestNow
        };
        dbContext.Players.Add(player);
        await dbContext.SaveChangesAsync();
        return player;
    }

    private static async Task<ProgressScopeSeed> SeedProgressScopeAsync(EventPlannerDbContext dbContext)
    {
        var player = await SeedPlayerAsync(dbContext);
        var eventDefinition = new EventDefinition
        {
            Slug = Guid.NewGuid().ToString("N"),
            Name = "Integration Event",
            EventType = "bingo",
            Status = EventStatuses.Active,
            StartsAt = TestNow.AddHours(-1),
            EndsAt = TestNow.AddHours(1),
            CreatedAt = TestNow
        };
        dbContext.Events.Add(eventDefinition);
        await dbContext.SaveChangesAsync();

        var team = new EventTeam
        {
            EventId = eventDefinition.Id,
            Name = "Blue",
            CreatedAt = TestNow
        };
        var board = new BingoBoard
        {
            EventId = eventDefinition.Id,
            Name = "Board",
            CreatedAt = TestNow
        };
        dbContext.EventTeams.Add(team);
        dbContext.BingoBoards.Add(board);
        await dbContext.SaveChangesAsync();

        var tile = new BingoTile
        {
            BoardId = board.Id,
            Title = "Tile",
            SortOrder = 1
        };
        dbContext.BingoTiles.Add(tile);
        await dbContext.SaveChangesAsync();

        var tier = new BingoTileTier
        {
            TileId = tile.Id,
            TierNumber = 1,
            Title = "Tier 1",
            IsRequiredForBoardCompletion = true,
            ScoreValue = 1
        };
        dbContext.BingoTileTiers.Add(tier);
        await dbContext.SaveChangesAsync();

        _ = player;
        return new ProgressScopeSeed(eventDefinition.Id, tile.Id, tier.Id, team.Id);
    }

    private static EventTileProgress CreateTileProgress(long eventId, long tileId, long teamId)
    {
        return new EventTileProgress
        {
            EventId = eventId,
            TileId = tileId,
            TeamId = teamId,
            PlayerId = null,
            CurrentValue = 1,
            UpdatedAt = TestNow,
            MetadataJson = JsonDefaults.Object
        };
    }

    private static EventTileTierProgress CreateTierProgress(long eventId, long tileId, long tierId, long teamId)
    {
        return new EventTileTierProgress
        {
            EventId = eventId,
            TileId = tileId,
            TileTierId = tierId,
            TeamId = teamId,
            PlayerId = null,
            CurrentValue = 1,
            UpdatedAt = TestNow,
            MetadataJson = JsonDefaults.Object
        };
    }

    private static async Task AddQueuedActivityAsync(EventPlannerDbContext dbContext, long playerId, string dedupeKey)
    {
        var activity = new ActivityEvent
        {
            PlayerId = playerId,
            ActivityType = ActivityTypes.ItemDrop,
            SourceSystem = "integration",
            SourceEndpoint = "/api/activity",
            Source = "Abyssal demons",
            ItemId = 4151,
            ItemName = "Abyssal whip",
            Quantity = 1,
            OccurredAt = TestNow,
            ReceivedAt = TestNow,
            RawPayloadJson = "{}",
            DedupeKey = dedupeKey
        };
        dbContext.ActivityEvents.Add(activity);
        await dbContext.SaveChangesAsync();

        dbContext.ActivityEventItems.Add(new ActivityEventItem
        {
            ActivityEventId = activity.Id,
            ItemId = 4151,
            ItemName = "Abyssal whip",
            Quantity = 1,
            Source = "Abyssal demons"
        });
        dbContext.ActivityProcessingQueue.Add(new ActivityProcessingQueueItem
        {
            ActivityEventId = activity.Id,
            Status = ActivityProcessingStatuses.Pending,
            AvailableAt = TestNow,
            CreatedAt = TestNow
        });
        await dbContext.SaveChangesAsync();
    }

    private sealed record ProgressScopeSeed(long EventId, long TileId, long TierId, long TeamId);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

public sealed class PostgreSqlIntegrationFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;

    public async Task InitializeAsync()
    {
        container = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    public async Task<EventPlannerDbContext> CreateMigratedDbContextAsync()
    {
        var connectionString = await CreateDatabaseAsync();
        var dbContext = CreateDbContext(connectionString);
        await dbContext.Database.MigrateAsync();
        return dbContext;
    }

    public EventPlannerDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<EventPlannerDbContext>()
            .UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(EventPlannerDbContext).Assembly.FullName))
            .Options;

        return new EventPlannerDbContext(options);
    }

    private async Task<string> CreateDatabaseAsync()
    {
        if (container is null)
        {
            throw new InvalidOperationException("PostgreSQL integration fixture has not been initialized.");
        }

        var databaseName = $"swedes_test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"""CREATE DATABASE "{databaseName}";""", connection);
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = databaseName
        };
        return builder.ConnectionString;
    }
}

public sealed class PostgreSqlIntegrationFactAttribute : FactAttribute
{
    public PostgreSqlIntegrationFactAttribute()
    {
        if (RequiresDocker())
        {
            return;
        }

        if (!IsDockerAvailable())
        {
            Skip = "PostgreSQL integration tests require Docker/Testcontainers.";
        }
    }

    private static bool RequiresDocker()
    {
        var value = Environment.GetEnvironmentVariable("SWEDES_RUN_POSTGRES_INTEGRATION");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDockerAvailable()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(dockerHost))
        {
            return true;
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var pipe = new System.IO.Pipes.NamedPipeClientStream(
                    ".",
                    "docker_engine",
                    System.IO.Pipes.PipeDirection.InOut);
                pipe.Connect(250);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        return File.Exists("/var/run/docker.sock");
    }
}
