using Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data;

public sealed class InfrastructureDbContext(
    DbContextOptions<InfrastructureDbContext> options,
    IConfiguration configuration) : DbContext(options)
{
    private readonly string _eventStoreSchema = configuration["EventStore:Schema"] ?? "public";

    public DbSet<EventRecord> DeviceEvents => Set<EventRecord>();

    public DbSet<UserRecord> Users => Set<UserRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventRecord>(entity =>
        {
            entity.ToTable("device_events", _eventStoreSchema);
            entity.HasKey(x => new { x.StreamId, x.Version });
            entity.Property(x => x.StreamId).HasColumnName("stream_id");
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.OccurredOnUtc).HasColumnName("occurred_on_utc").IsRequired();
            entity.HasIndex(x => x.OccurredOnUtc).HasDatabaseName("ix_device_events_occurred_on_utc");
        });

        modelBuilder.Entity<UserRecord>(entity =>
        {
            entity.ToTable("users", "public");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Username).HasColumnName("username").IsRequired();
            entity.Property(x => x.NormalizedUsername).HasColumnName("normalized_username").IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(x => x.Role).HasColumnName("role").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.HasIndex(x => x.NormalizedUsername)
                .IsUnique()
                .HasDatabaseName("ix_users_normalized_username");
        });
    }
}
