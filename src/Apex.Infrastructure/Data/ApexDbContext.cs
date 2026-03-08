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

    protected override void OnModelCreating(ModelBuilder m)
    {
        // Sessions
        m.Entity<Session>(e =>
        {
            e.HasKey(x => x.SessionId);
            e.Property(x => x.StartTime).HasDefaultValueSql("GETDATE()");
            e.Property(x => x.IsConsolidated).HasDefaultValue(false);
            e.Property(x => x.ContextHash).HasMaxLength(64);
        });

        // Messages
        m.Entity<Message>(e =>
        {
            e.HasKey(x => x.MessageId);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Timestamp).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.SessionId);
        });

        // Suggestions
        m.Entity<Suggestion>(e =>
        {
            e.HasKey(x => x.SuggestionId);
            e.Property(x => x.SuggestionType).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.ExtractionMethod).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ExtractionMethod.Regex);
            e.Property(x => x.Language).HasMaxLength(50);
            e.Property(x => x.FilePath).HasMaxLength(255);
            e.Property(x => x.IsApplied).HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.MessageId);
        });

        // Outcomes
        m.Entity<Outcome>(e =>
        {
            e.HasKey(x => x.OutcomeId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(OutcomeStatus.Pending);
            e.Property(x => x.ErrorCode).HasMaxLength(100);
            e.Property(x => x.ConfirmedByGit).HasDefaultValue(false);
            e.Property(x => x.IsExplicit).HasDefaultValue(false);
            e.Property(x => x.FeedbackAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.SuggestionId);
        });

        // Preferences
        m.Entity<Preference>(e =>
        {
            e.HasKey(x => x.PrefId);
            e.Property(x => x.Category).HasMaxLength(50);
            e.Property(x => x.Key).HasColumnName("Key").HasMaxLength(100);
            e.Property(x => x.ConfidenceScore).HasDefaultValue(0.5);
            e.Property(x => x.ReinforcementCount).HasDefaultValue(0);
            e.Property(x => x.IsExplicit).HasDefaultValue(false);
            e.Property(x => x.LastUpdated).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => new { x.Category, x.Key }).IsUnique();
        });

        // AntiPatterns
        m.Entity<AntiPattern>(e =>
        {
            e.HasKey(x => x.AntiPatternId);
            e.Property(x => x.Pattern).HasMaxLength(500);
            e.Property(x => x.Language).HasMaxLength(50);
            e.Property(x => x.ErrorCode).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        // Tasks
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
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        // TaskSteps
        m.Entity<TaskStep>(e =>
        {
            e.HasKey(x => x.StepId);
            e.Property(x => x.StepName).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(TaskStepStatus.Pending);
            e.Property(x => x.FilePath).HasMaxLength(500);
            e.Property(x => x.StartedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.TaskId);
        });

        // AuditLog
        m.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLog");
            e.HasKey(x => x.AuditId);
            e.Property(x => x.DetectedType).HasMaxLength(50);
            e.Property(x => x.FilePathHash).HasMaxLength(64);
            e.Property(x => x.FindingCount).HasDefaultValue(0);
            e.Property(x => x.WasBlocked).HasDefaultValue(false);
            e.Property(x => x.WasRedacted).HasDefaultValue(false);
            e.Property(x => x.AuditedAt).HasDefaultValueSql("GETDATE()");
        });

        // Experiments
        m.Entity<Experiment>(e =>
        {
            e.HasKey(x => x.ExperimentId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Tier).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ExperimentStatus.Active);
            e.Property(x => x.TargetSessions).HasDefaultValue(60);
            e.Property(x => x.StartedAt).HasDefaultValueSql("GETDATE()");
            e.Property(x => x.WinnerFormat).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(x => new { x.Tier, x.Status }).IsUnique();
        });

        // ExperimentAssignments
        m.Entity<ExperimentAssignment>(e =>
        {
            e.HasKey(x => x.AssignmentId);
            e.Property(x => x.Format).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Tier).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.AssignedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => new { x.SessionId, x.Tier }).IsUnique();
        });

        // ExperimentMetrics
        m.Entity<ExperimentMetrics>(e =>
        {
            e.HasKey(x => x.MetricId);
            e.Property(x => x.SuggestionCount).HasDefaultValue(0);
            e.Property(x => x.SuggestionsApplied).HasDefaultValue(0);
            e.Property(x => x.OutcomesWorked).HasDefaultValue(0);
            e.Property(x => x.OutcomesFailed).HasDefaultValue(0);
            e.Property(x => x.CorrectionCount).HasDefaultValue(0);
            e.Property(x => x.RepeatExplanationCount).HasDefaultValue(0);
            e.Property(x => x.ComputedAt).HasDefaultValueSql("GETDATE()");
        });

        // Projects
        m.Entity<Project>(e =>
        {
            e.HasKey(x => x.ProjectId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.Keywords).HasMaxLength(1000).HasDefaultValue("");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.Name).IsUnique();
            e.Ignore(x => x.KeywordList);
        });

        // SystemSettings
        m.Entity<SystemSetting>(e =>
        {
            e.ToTable("SystemSettings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(500);
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("GETDATE()");
        });
    }
}
