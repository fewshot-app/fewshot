using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StarkTrace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AntiPatterns",
                columns: table => new
                {
                    AntiPatternId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntiPatterns", x => x.AntiPatternId);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    AuditId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectedType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FilePathHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    FindingCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    WasBlocked = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    WasRedacted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AuditedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "ExperimentAssignments",
                columns: table => new
                {
                    AssignmentId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExperimentId = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Tier = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    TokensUsed = table.Column<int>(type: "INTEGER", nullable: true),
                    TokenBudget = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentAssignments", x => x.AssignmentId);
                });

            migrationBuilder.CreateTable(
                name: "ExperimentMetrics",
                columns: table => new
                {
                    MetricId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssignmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SuggestionCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    SuggestionsApplied = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    OutcomesWorked = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    OutcomesFailed = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CorrectionCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    RepeatExplanationCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalTokensIn = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalTokensOut = table.Column<int>(type: "INTEGER", nullable: true),
                    SessionDurationMinutes = table.Column<double>(type: "REAL", nullable: true),
                    MessagesToFirstUseful = table.Column<int>(type: "INTEGER", nullable: true),
                    ApiCostCents = table.Column<double>(type: "REAL", nullable: true),
                    EffortSavedMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentMetrics", x => x.MetricId);
                });

            migrationBuilder.CreateTable(
                name: "Experiments",
                columns: table => new
                {
                    ExperimentId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tier = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Active"),
                    TargetSessions = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 60),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    ConcludedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WinnerFormat = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Conclusion = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Experiments", x => x.ExperimentId);
                });

            migrationBuilder.CreateTable(
                name: "LicenseActivations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LicenseKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PackId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DecryptionKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseActivations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Memories",
                columns: table => new
                {
                    PointId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Project = table.Column<string>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Solution = table.Column<string>(type: "TEXT", nullable: true),
                    Approach = table.Column<string>(type: "TEXT", nullable: true),
                    OutcomeLabel = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    Language = table.Column<string>(type: "TEXT", nullable: true),
                    Embedding = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memories", x => x.PointId);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "Outcomes",
                columns: table => new
                {
                    OutcomeId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SuggestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EffortSavedMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    ConfirmedByGit = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsExplicit = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    FeedbackAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outcomes", x => x.OutcomeId);
                });

            migrationBuilder.CreateTable(
                name: "Preferences",
                columns: table => new
                {
                    PrefId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.5),
                    ReinforcementCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    IsExplicit = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    SourceSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Preferences", x => x.PrefId);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false, defaultValue: ""),
                    Facts = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.ProjectId);
                });

            migrationBuilder.CreateTable(
                name: "ProxyAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Direction = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FindingTypes = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FindingCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxConfidence = table.Column<double>(type: "REAL", nullable: false),
                    WasRedacted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snippet = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProxyAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Project = table.Column<string>(type: "TEXT", nullable: true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ContextHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsConsolidated = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ConsolidatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConsolidationSummary = table.Column<string>(type: "TEXT", nullable: true),
                    ConsolidationError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "Suggestions",
                columns: table => new
                {
                    SuggestionId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    SuggestionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ExtractionMethod = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Regex"),
                    ExtractionConfidence = table.Column<double>(type: "REAL", nullable: true),
                    IsApplied = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suggestions", x => x.SuggestionId);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    TaskId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Queued"),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    Result = table.Column<string>(type: "TEXT", nullable: true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    MaxAttempts = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 3),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    LockedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LockedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.TaskId);
                });

            migrationBuilder.CreateTable(
                name: "TaskSteps",
                columns: table => new
                {
                    StepId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Output = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskSteps", x => x.StepId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentAssignments_SessionId_Tier",
                table: "ExperimentAssignments",
                columns: new[] { "SessionId", "Tier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Experiments_Tier_Status",
                table: "Experiments",
                columns: new[] { "Tier", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicenseActivations_LicenseKey",
                table: "LicenseActivations",
                column: "LicenseKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memories_SessionId",
                table: "Memories",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SessionId",
                table: "Messages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Outcomes_SuggestionId",
                table: "Outcomes",
                column: "SuggestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Preferences_Category_Key",
                table: "Preferences",
                columns: new[] { "Category", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProxyAuditLogs_Timestamp",
                table: "ProxyAuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ProxyAuditLogs_WasRedacted",
                table: "ProxyAuditLogs",
                column: "WasRedacted");

            migrationBuilder.CreateIndex(
                name: "IX_Suggestions_MessageId",
                table: "Suggestions",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskSteps_TaskId",
                table: "TaskSteps",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AntiPatterns");

            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "ExperimentAssignments");

            migrationBuilder.DropTable(
                name: "ExperimentMetrics");

            migrationBuilder.DropTable(
                name: "Experiments");

            migrationBuilder.DropTable(
                name: "LicenseActivations");

            migrationBuilder.DropTable(
                name: "Memories");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Outcomes");

            migrationBuilder.DropTable(
                name: "Preferences");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "ProxyAuditLogs");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Suggestions");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "TaskSteps");
        }
    }
}
