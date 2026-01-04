using Microsoft.EntityFrameworkCore;
using MahjongStats.Models;

namespace MahjongStats.Data;

public class MahjongStatsContext : DbContext
{
    public MahjongStatsContext(DbContextOptions<MahjongStatsContext> options) : base(options)
    {
    }

    public DbSet<StoredGame> StoredGames { get; set; }
    public DbSet<StoredRound> StoredRounds { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure StoredGame
        modelBuilder.Entity<StoredGame>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.GameId).IsRequired();
            entity.Property(e => e.Players).IsRequired();
            entity.Property(e => e.PointsJson).IsRequired();
            entity.Property(e => e.CreatedDateTime)
                .IsRequired()
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.Property(e => e.FetchedDateTime)
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.HasIndex(e => e.GameId).IsUnique();
            entity.HasIndex(e => e.CreatedDateTime);
        });

        // Configure StoredRound
        modelBuilder.Entity<StoredRound>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GameId).IsRequired();
            entity.Property(e => e.RoundJson).IsRequired();
            entity.Property(e => e.CreatedDateTime)
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.HasIndex(e => e.GameId);
        });
    }
}
