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
        modelBuilder.Entity<PlayerModel>().ToTable("players");
        modelBuilder.Entity<TournamentModel>().ToTable("tournaments");
        modelBuilder.Entity<MatchSummaryModel>().ToTable("match_summaries");
        modelBuilder.Entity<LegModel>().ToTable("legs");
        modelBuilder.Entity<LegDetailModel>().ToTable("leg_details");

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
