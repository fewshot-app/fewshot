using Apex.Core.Enums;
using Apex.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Apex.Infrastructure.Data;

public class ApexDbContext : DbContext
{
    public ApexDbContext(DbContextOptions<ApexDbContext> options) : base(options) { }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Suggestion> Suggestions => Set<Suggestion>();
    public DbSet<Outcome> Outcomes => Set<Outcome>();
    public DbSet<Preference> Preferences => Set<Preference>();
    public DbSet<AntiPattern> AntiPatterns => Set<AntiPattern>();
    public DbSet<ApexTask> Tasks => Set<ApexTask>();
    public DbSet<TaskStep> TaskSteps => Set<TaskStep>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Experiment> Experiments => Set<Experiment>();
    public DbSet<ExperimentAssignment> ExperimentAssignments => Set<ExperimentAssignment>();
    public DbSet<ExperimentMetrics> ExperimentMetrics => Set<ExperimentMetrics>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<MemoryEntry> Memories => Set<MemoryEntry>();
    public DbSet<ProxyAuditLog> ProxyAuditLogs => Set<ProxyAuditLog>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Session>(e =>
        {
            e.HasKey(x => x.SessionId);
            e.Property(x => x.StartTime).HasDefaultValueSql("datetime('now')");
            e.Property(x => x.IsConsolidated).HasDefaultValue(false);
            e.Property(x => x.ContextHash).HasMaxLength(64);
        });

        m.Entity<Message>(e =>
        {
            e.HasKey(x => x.MessageId);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Timestamp).HasDefaultValueSql("datetime('now')");
            e.HasIndex(x => x.SessionId);
        });

        m.Entity<Suggestion>(e =>
        {
            e.HasKey(x => x.SuggestionId);
            e.Property(x => x.SuggestionType).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.ExtractionMethod).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ExtractionMethod.Regex);
            e.Property(x => x.Language).HasMaxLength(50);
            e.Property(x => x.FilePath).HasMaxLength(255);
            e.Property(x => x.IsApplied).HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
            e.HasIndex(x => x.MessageId);
        });

        m.Entity<Outcome>(e =>
        {
            e.HasKey(x => x.OutcomeId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(OutcomeStatus.Pending);
            e.Property(x => x.ErrorCode).HasMaxLength(100);
            e.Property(x => x.ConfirmedByGit).HasDefaultValue(false);
            e.Property(x => x.IsExplicit).HasDefaultValue(false);
            e.Property(x => x.FeedbackAt).HasDefaultValueSql("datetime('now')");
            e.HasIndex(x => x.SuggestionId);
        });

        m.Entity<Preference>(e =>
        {
            e.HasKey(x => x.PrefId);
            e.Property(x => x.Category).HasMaxLength(50);
            e.Property(x => x.Key).HasColumnName("Key").HasMaxLength(100);
            e.Property(x => x.ConfidenceScore).HasDefaultValue(0.5);
            e.Property(x => x.ReinforcementCount).HasDefaultValue(0);
            e.Property(x => x.IsExplicit).HasDefaultValue(false);
            e.Property(x => x.LastUpdated).HasDefaultValueSql("datetime('now')");
            e.HasIndex(x => new { x.Category, x.Key }).IsUnique();
        });

        m.Entity<AntiPattern>(e =>
        {
            e.HasKey(x => x.AntiPatternId);
            e.Property(x => x.Pattern).HasMaxLength(500);
            e.Property(x => x.Language).HasMaxLength(50);
            e.Property(x => x.ErrorCode).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        m.Entity<ApexTask>(e =>
        {
            e.ToTable("Tasks");
            e.HasKey(x => x.TaskId);
            e.Property(x => x.TaskType).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Queued");
            e.Property(x => x.MaxAttempts).HasDefaultValue(3);
            e.Property(x => x.AttemptCount).HasDefaultValue(0);
            e.Property(x => x.RequiresApproval).HasDefaultValue(false);
            e.Property(x => x.LockedBy).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        m.Entity<TaskStep>(e =>
        {
            e.HasKey(x => x.StepId);
            e.Property(x => x.StepName).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(TaskStepStatus.Pending);
            e.Property(x => x.FilePath).HasMaxLength(500);
            e.Property(x => x.StartedAt).HasDefaultValueSql("datetime('now')");
            e.HasIndex(x => x.TaskId);
        });

        m.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLog");
            e.HasKey(x => x.AuditId);
            e.Property(x => x.DetectedType).HasMaxLength(50);
            e.Property(x => x.FilePathHash).HasMaxLength(64);
            e.Property(x => x.FindingCount).HasDefaultValue(0);
            e.Property(x => x.WasBlocked).HasDefaultValue(false);
            e.Property(x => x.WasRedacted).HasDefaultValue(false);
            e.Property(x => x.AuditedAt).HasDefaultValueSql("datetime('now')");
        });

        m.Entity<Experiment>(e =>
        {
            e.HasKey(x => x.ExperimentId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Tier).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ExperimentStatus.Active);
            e.Property(x => x.TargetSessions).HasDefaultValue(60);
            e.Property(x => x.StartedAt).HasDefaultValueSql("datetime('now')");
            e.Property(x => x.WinnerFormat).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(x => new { x.Tier, x.Status }).IsUnique();
        });

        m.Entity<ExperimentAssignment>(e =>
        {
            e.HasKey(x => x.AssignmentId);
            e.Property(x => x.Format).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Tier).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.AssignedAt).HasDefaultValueSql("datetime('now')");
            e.HasIndex(x => new { x.SessionId, x.Tier }).IsUnique();
        });

        m.Entity<ExperimentMetrics>(e =>
        {
            e.HasKey(x => x.MetricId);
            e.Property(x => x.SuggestionCount).HasDefaultValue(0);
            e.Property(x => x.SuggestionsApplied).HasDefaultValue(0);
            e.Property(x => x.OutcomesWorked).HasDefaultValue(0);
            e.Property(x => x.OutcomesFailed).HasDefaultValue(0);
            e.Property(x => x.CorrectionCount).HasDefaultValue(0);
            e.Property(x => x.RepeatExplanationCount).HasDefaultValue(0);
            e.Property(x => x.ComputedAt).HasDefaultValueSql("datetime('now')");
        });

        m.Entity<Project>(e =>
        {
            e.HasKey(x => x.ProjectId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.Keywords).HasMaxLength(1000).HasDefaultValue("");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
            e.HasIndex(x => x.Name).IsUnique();
            e.Ignore(x => x.KeywordList);
        });

        m.Entity<SystemSetting>(e =>
        {
            e.ToTable("SystemSettings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(500);
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("datetime('now')");
        });

        // Memories — embedding stored as BLOB (byte[])
        m.Entity<MemoryEntry>(e =>
        {
            e.HasKey(x => x.PointId);
            e.Property(x => x.PointId).HasMaxLength(36);
            e.Property(x => x.Summary).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("datetime('now')");
            e.HasIndex(x => x.SessionId);
        });

        // ProxyAuditLog — events from Apex.Proxy stdio interceptor
        m.Entity<ProxyAuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Direction).HasMaxLength(20);
            e.Property(x => x.Method).HasMaxLength(100);
            e.Property(x => x.FindingTypes).HasMaxLength(200);
            e.Property(x => x.Source).HasMaxLength(50);
            e.Property(x => x.Timestamp).HasDefaultValueSql("datetime('now')");
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.WasRedacted);
        });
    }
}
