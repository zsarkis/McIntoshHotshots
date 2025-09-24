using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using McIntoshHotshots.Model;

namespace McIntoshHotshots.db;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<PlayerModel> Players { get; set; }
    public DbSet<TournamentModel> Tournaments { get; set; }
    public DbSet<MatchSummaryModel> MatchSummaries { get; set; }
    public DbSet<LegModel> Legs { get; set; }
    public DbSet<LegDetailModel> LegDetails { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure table names to match existing database schema
        modelBuilder.Entity<PlayerModel>().ToTable("player");
        modelBuilder.Entity<TournamentModel>().ToTable("tournament");
        modelBuilder.Entity<MatchSummaryModel>().ToTable("match_summary");
        modelBuilder.Entity<LegModel>().ToTable("leg");
        modelBuilder.Entity<LegDetailModel>().ToTable("leg_detail");

        // Configure column names to match existing database schema (lowercase)
        modelBuilder.Entity<PlayerModel>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Earnings).HasColumnName("earnings");
            // elo_number and user_id are already mapped via [Column] attribute
            // preferences is already mapped via [Column] attribute
        });

        modelBuilder.Entity<TournamentModel>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Date).HasColumnName("date");
            // pool_count and max_attendees are already mapped via [Column] attribute
        });

        modelBuilder.Entity<MatchSummaryModel>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("id");
            // Other columns are already mapped via [Column] attributes
        });

        modelBuilder.Entity<LegModel>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.LegNumber).HasColumnName("leg_number");
            entity.Property(e => e.HomePlayerDartsThrown).HasColumnName("home_player_darts_thrown");
            entity.Property(e => e.AwayPlayerDartsThrown).HasColumnName("away_player_darts_thrown");
            entity.Property(e => e.LoserScoreRemaining).HasColumnName("loser_score_remaining");
            entity.Property(e => e.WinnerId).HasColumnName("winner_id");
            entity.Property(e => e.TimeElapsed).HasColumnName("time_elapsed");
        });

        modelBuilder.Entity<LegDetailModel>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.LegId).HasColumnName("leg_id");
            entity.Property(e => e.TurnNumber).HasColumnName("turn_number");
            entity.Property(e => e.PlayerId).HasColumnName("player_id");
            entity.Property(e => e.ScoreRemainingBeforeThrow).HasColumnName("score_remaining_before_throw");
            entity.Property(e => e.Score).HasColumnName("score");
            entity.Property(e => e.DartsUsed).HasColumnName("darts_used");
        });

        // Configure foreign key relationships
        modelBuilder.Entity<MatchSummaryModel>()
            .HasOne<PlayerModel>()
            .WithMany()
            .HasForeignKey(m => m.HomePlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MatchSummaryModel>()
            .HasOne<PlayerModel>()
            .WithMany()
            .HasForeignKey(m => m.AwayPlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MatchSummaryModel>()
            .HasOne<TournamentModel>()
            .WithMany()
            .HasForeignKey(m => m.TournamentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<LegModel>()
            .HasOne<MatchSummaryModel>()
            .WithMany()
            .HasForeignKey(l => l.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LegDetailModel>()
            .HasOne<MatchSummaryModel>()
            .WithMany()
            .HasForeignKey(ld => ld.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LegDetailModel>()
            .HasOne<LegModel>()
            .WithMany()
            .HasForeignKey(ld => ld.LegId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
