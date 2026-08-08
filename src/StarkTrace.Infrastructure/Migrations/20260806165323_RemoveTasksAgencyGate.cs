using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StarkTrace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTasksAgencyGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "TaskSteps");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    TaskId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LockedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MaxAttempts = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 3),
                    NextRetryAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Result = table.Column<string>(type: "TEXT", nullable: true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Queued"),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
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
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Output = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    StepName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TaskId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskSteps", x => x.StepId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskSteps_TaskId",
                table: "TaskSteps",
                column: "TaskId");
        }
    }
}
