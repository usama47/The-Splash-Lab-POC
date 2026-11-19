using Microsoft.EntityFrameworkCore;
using TheSplashLab.Core.Entities;

namespace TheSplashLab.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DemoUser> Users => Set<DemoUser>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingDraft> MeetingDrafts => Set<MeetingDraft>();
    public DbSet<DraftDecision> DraftDecisions => Set<DraftDecision>();
    public DbSet<DraftCommitment> DraftCommitments => Set<DraftCommitment>();
    public DbSet<DraftIssue> DraftIssues => Set<DraftIssue>();
    public DbSet<DecisionRecord> Decisions => Set<DecisionRecord>();
    public DbSet<CommitmentRecord> Commitments => Set<CommitmentRecord>();
    public DbSet<CommitmentStatusHistory> CommitmentStatusHistories => Set<CommitmentStatusHistory>();
    public DbSet<DestinationTask> DestinationTasks => Set<DestinationTask>();
    public DbSet<IssueRecord> Issues => Set<IssueRecord>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<ProcessRegisterEntry> ProcessRegister => Set<ProcessRegisterEntry>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<IntegrationSetting> IntegrationSettings => Set<IntegrationSetting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Guid>().HaveConversion<GuidUpperStringConverter>();
        configurationBuilder.Properties<Guid?>().HaveConversion<NullableGuidUpperStringConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Meeting>()
            .HasOne(m => m.Draft)
            .WithOne(d => d.Meeting)
            .HasForeignKey<MeetingDraft>(d => d.MeetingId);

        modelBuilder.Entity<MeetingDraft>()
            .HasMany(d => d.Decisions)
            .WithOne(x => x.MeetingDraft)
            .HasForeignKey(x => x.MeetingDraftId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MeetingDraft>()
            .HasMany(d => d.Commitments)
            .WithOne(x => x.MeetingDraft)
            .HasForeignKey(x => x.MeetingDraftId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MeetingDraft>()
            .HasMany(d => d.Issues)
            .WithOne(x => x.MeetingDraft)
            .HasForeignKey(x => x.MeetingDraftId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CommitmentRecord>()
            .HasOne(c => c.DestinationTask)
            .WithOne(t => t.Commitment)
            .HasForeignKey<DestinationTask>(t => t.CommitmentId);

        modelBuilder.Entity<CommitmentRecord>()
            .HasMany(c => c.StatusHistory)
            .WithOne(h => h.Commitment)
            .HasForeignKey(h => h.CommitmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DecisionRecord>()
            .HasIndex(d => d.IdempotencyKey)
            .IsUnique();

        modelBuilder.Entity<CommitmentRecord>()
            .HasIndex(c => c.IdempotencyKey)
            .IsUnique();

        modelBuilder.Entity<DestinationTask>()
            .HasIndex(t => t.IdempotencyKey)
            .IsUnique();

        modelBuilder.Entity<AuditLogEntry>()
            .Property(a => a.Id)
            .ValueGeneratedOnAdd();

        // Append-only: no cascade delete from meetings into audit
        modelBuilder.Entity<AuditLogEntry>().HasKey(a => a.Id);
    }
}
