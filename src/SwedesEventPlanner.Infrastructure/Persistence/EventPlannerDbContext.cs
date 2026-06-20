using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.Players;

namespace SwedesEventPlanner.Infrastructure.Persistence;

public sealed class EventPlannerDbContext(DbContextOptions<EventPlannerDbContext> options)
    : DbContext(options)
{
    public DbSet<EventDefinition> Events => Set<EventDefinition>();

    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("players");

            entity.HasKey(player => player.Id);

            entity.Property(player => player.Id).HasColumnName("id");
            entity.Property(player => player.DisplayName).HasColumnName("display_name");
            entity.Property(player => player.RuneScapeName).HasColumnName("runescape_name");
            entity.Property(player => player.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(player => player.RuneScapeName).IsUnique();
        });

        modelBuilder.Entity<EventDefinition>(entity =>
        {
            entity.ToTable("events");

            entity.HasKey(eventDefinition => eventDefinition.Id);

            entity.Property(eventDefinition => eventDefinition.Id).HasColumnName("id");
            entity.Property(eventDefinition => eventDefinition.Slug).HasColumnName("slug");
            entity.Property(eventDefinition => eventDefinition.Name).HasColumnName("name");
            entity.Property(eventDefinition => eventDefinition.EventType).HasColumnName("event_type");
            entity.Property(eventDefinition => eventDefinition.Status).HasColumnName("status");
            entity.Property(eventDefinition => eventDefinition.StartsAt).HasColumnName("starts_at");
            entity.Property(eventDefinition => eventDefinition.EndsAt).HasColumnName("ends_at");
            entity.Property(eventDefinition => eventDefinition.TimeZone).HasColumnName("time_zone");
            entity.Property(eventDefinition => eventDefinition.CreatedAt).HasColumnName("created_at");
            entity.Property(eventDefinition => eventDefinition.ConfigJson)
                .HasColumnName("config_json")
                .HasColumnType("jsonb");

            entity.HasIndex(eventDefinition => eventDefinition.Slug).IsUnique();
            entity.HasIndex(eventDefinition => new
            {
                eventDefinition.Status,
                eventDefinition.StartsAt,
                eventDefinition.EndsAt,
            });
        });
    }
}
