-- Projects: dynamic project registry for StarkTrace session management
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Projects')
BEGIN
    CREATE TABLE Projects (
        ProjectId   INT NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Name        NVARCHAR(100) NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        Keywords    NVARCHAR(1000) NOT NULL DEFAULT '',
        Facts       NVARCHAR(MAX) NULL,
        IsActive    BIT NOT NULL DEFAULT 1,
        CreatedAt   DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_Projects_Name UNIQUE (Name)
    );

    -- Seed with a general/default project
    INSERT INTO Projects (Name, DisplayName, Keywords, Facts) VALUES
    ('general', 'General', 'general,misc,other', NULL);
END
