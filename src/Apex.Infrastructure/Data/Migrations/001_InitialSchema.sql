-- APEX Schema v2.0
-- Run against SQL Server 2022

CREATE TABLE Sessions (
    SessionId               INT IDENTITY PRIMARY KEY,
    StartTime               DATETIME NOT NULL DEFAULT GETDATE(),
    EndTime                 DATETIME NULL,
    ContextHash             NVARCHAR(64) NULL,
    IsConsolidated          BIT NOT NULL DEFAULT 0,
    ConsolidatedAt          DATETIME NULL,
    ConsolidationSummary    NVARCHAR(MAX) NULL,
    ConsolidationError      NVARCHAR(MAX) NULL
);

CREATE TABLE Messages (
    MessageId   INT IDENTITY PRIMARY KEY,
    SessionId   INT NOT NULL REFERENCES Sessions(SessionId),
    Role        NVARCHAR(20) NOT NULL,
    Content     NVARCHAR(MAX) NOT NULL,
    Timestamp   DATETIME NOT NULL DEFAULT GETDATE(),
    TokenCount  INT NULL
);
CREATE INDEX IX_Messages_SessionId ON Messages(SessionId);

CREATE TABLE Suggestions (
    SuggestionId            INT IDENTITY PRIMARY KEY,
    MessageId               INT NOT NULL REFERENCES Messages(MessageId),
    SuggestionType          NVARCHAR(50) NOT NULL,
    Content                 NVARCHAR(MAX) NOT NULL,
    Language                NVARCHAR(50) NULL,
    FilePath                NVARCHAR(255) NULL,
    ExtractionMethod        NVARCHAR(20) NOT NULL DEFAULT 'Regex',
    ExtractionConfidence    FLOAT NULL,
    IsApplied               BIT NOT NULL DEFAULT 0,
    AppliedAt               DATETIME NULL,
    Metadata                NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME NOT NULL DEFAULT GETDATE()
);
CREATE INDEX IX_Suggestions_MessageId ON Suggestions(MessageId);

CREATE TABLE Outcomes (
    OutcomeId           INT IDENTITY PRIMARY KEY,
    SuggestionId        INT NOT NULL REFERENCES Suggestions(SuggestionId),
    Status              NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    Notes               NVARCHAR(MAX) NULL,
    ErrorCode           NVARCHAR(100) NULL,
    EffortSavedMinutes  INT NULL,
    ConfirmedByGit      BIT NOT NULL DEFAULT 0,
    IsExplicit          BIT NOT NULL DEFAULT 0,
    FeedbackAt          DATETIME NOT NULL DEFAULT GETDATE()
);
CREATE INDEX IX_Outcomes_SuggestionId ON Outcomes(SuggestionId);

CREATE TABLE Preferences (
    PrefId              INT IDENTITY PRIMARY KEY,
    Category            NVARCHAR(50) NOT NULL,
    [Key]               NVARCHAR(100) NOT NULL,
    Value               NVARCHAR(MAX) NOT NULL,
    ConfidenceScore     FLOAT NOT NULL DEFAULT 0.5,
    ReinforcementCount  INT NOT NULL DEFAULT 0,
    IsExplicit          BIT NOT NULL DEFAULT 0,
    SourceSessionId     INT NULL REFERENCES Sessions(SessionId),
    LastUpdated         DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Preferences_CategoryKey UNIQUE (Category, [Key])
);

CREATE TABLE AntiPatterns (
    AntiPatternId   INT IDENTITY PRIMARY KEY,
    SessionId       INT NOT NULL REFERENCES Sessions(SessionId),
    Pattern         NVARCHAR(500) NOT NULL,
    Reason          NVARCHAR(MAX) NOT NULL,
    Language        NVARCHAR(50) NULL,
    ErrorCode       NVARCHAR(100) NULL,
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Tasks (
    TaskId              INT IDENTITY PRIMARY KEY,
    SessionId           INT NOT NULL REFERENCES Sessions(SessionId),
    TaskType            NVARCHAR(50) NOT NULL,
    Status              NVARCHAR(20) NOT NULL DEFAULT 'Queued',
    Payload             NVARCHAR(MAX) NOT NULL,
    Result              NVARCHAR(MAX) NULL,
    AttemptCount        INT NOT NULL DEFAULT 0,
    MaxAttempts         INT NOT NULL DEFAULT 3,
    LastError           NVARCHAR(MAX) NULL,
    LockedBy            NVARCHAR(100) NULL,
    LockedAt            DATETIME NULL,
    NextRetryAt         DATETIME NULL,
    RequiresApproval    BIT NOT NULL DEFAULT 0,
    CreatedAt           DATETIME NOT NULL DEFAULT GETDATE(),
    StartedAt           DATETIME NULL,
    CompletedAt         DATETIME NULL
);

CREATE TABLE TaskSteps (
    StepId      INT IDENTITY PRIMARY KEY,
    TaskId      INT NOT NULL REFERENCES Tasks(TaskId),
    StepName    NVARCHAR(100) NOT NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    FilePath    NVARCHAR(500) NULL,
    StartedAt   DATETIME NOT NULL DEFAULT GETDATE(),
    CompletedAt DATETIME NULL,
    Output      NVARCHAR(MAX) NULL
);
CREATE INDEX IX_TaskSteps_TaskId ON TaskSteps(TaskId);

CREATE TABLE AuditLog (
    AuditId         INT IDENTITY PRIMARY KEY,
    SessionId       INT NOT NULL REFERENCES Sessions(SessionId),
    DetectedType    NVARCHAR(50) NOT NULL,
    FilePathHash    NVARCHAR(64) NULL,
    FindingCount    INT NOT NULL DEFAULT 0,
    WasBlocked      BIT NOT NULL DEFAULT 0,
    WasRedacted     BIT NOT NULL DEFAULT 0,
    AuditedAt       DATETIME NOT NULL DEFAULT GETDATE()
);

-- Experiment A/B Testing Tables
CREATE TABLE Experiments (
    ExperimentId    INT IDENTITY PRIMARY KEY,
    Name            NVARCHAR(100) NOT NULL,
    Tier            NVARCHAR(10) NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Active',
    TargetSessions  INT NOT NULL DEFAULT 60,
    StartedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    ConcludedAt     DATETIME NULL,
    WinnerFormat    NVARCHAR(10) NULL,
    Conclusion      NVARCHAR(MAX) NULL,
    CONSTRAINT UQ_Experiments_ActiveTier UNIQUE (Tier, Status)
);

CREATE TABLE ExperimentAssignments (
    AssignmentId    INT IDENTITY PRIMARY KEY,
    ExperimentId    INT NOT NULL REFERENCES Experiments(ExperimentId),
    SessionId       INT NOT NULL REFERENCES Sessions(SessionId),
    Format          NVARCHAR(10) NOT NULL,
    Tier            NVARCHAR(10) NOT NULL,
    TokensUsed      INT NULL,
    TokenBudget     INT NULL,
    AssignedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Assignment_SessionTier UNIQUE (SessionId, Tier)
);

CREATE TABLE ExperimentMetrics (
    MetricId                INT IDENTITY PRIMARY KEY,
    AssignmentId            INT NOT NULL REFERENCES ExperimentAssignments(AssignmentId),
    SessionId               INT NOT NULL REFERENCES Sessions(SessionId),
    SuggestionCount         INT NOT NULL DEFAULT 0,
    SuggestionsApplied      INT NOT NULL DEFAULT 0,
    OutcomesWorked          INT NOT NULL DEFAULT 0,
    OutcomesFailed          INT NOT NULL DEFAULT 0,
    CorrectionCount         INT NOT NULL DEFAULT 0,
    RepeatExplanationCount  INT NOT NULL DEFAULT 0,
    TotalTokensIn           INT NULL,
    TotalTokensOut          INT NULL,
    SessionDurationMinutes  FLOAT NULL,
    MessagesToFirstUseful   INT NULL,
    ApiCostCents            FLOAT NULL,
    EffortSavedMinutes      INT NULL,
    ComputedAt              DATETIME NOT NULL DEFAULT GETDATE()
);
