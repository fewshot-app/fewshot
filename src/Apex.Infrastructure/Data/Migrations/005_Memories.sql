-- SQL Server 2025 native vector support
-- VECTOR(768) stores 768-dimensional float32 embeddings (nomic-embed-text)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Memories')
BEGIN
    CREATE TABLE Memories (
        MemoryId        INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PointId         NVARCHAR(36) NOT NULL,           -- GUID, kept for API compat
        SessionId       INT NOT NULL,
        Project         NVARCHAR(100) NULL,
        Summary         NVARCHAR(2000) NOT NULL,
        Solution        NVARCHAR(MAX) NULL,
        Approach        NVARCHAR(MAX) NULL,
        OutcomeLabel    NVARCHAR(100) NULL,
        Tags            NVARCHAR(500) NULL,
        Language        NVARCHAR(50) NULL,
        Embedding       VECTOR(768) NOT NULL,
        CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_Memories_PointId UNIQUE (PointId)
    );

    CREATE INDEX IX_Memories_SessionId ON Memories (SessionId);
    CREATE INDEX IX_Memories_Project   ON Memories (Project);
END
