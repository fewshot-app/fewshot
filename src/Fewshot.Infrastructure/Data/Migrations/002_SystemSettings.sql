-- SystemSettings: key-value store for runtime-configurable settings
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemSettings')
BEGIN
    CREATE TABLE SystemSettings (
        [Key]       NVARCHAR(100) NOT NULL PRIMARY KEY,
        [Value]     NVARCHAR(500) NOT NULL,
        UpdatedAt   DATETIME NOT NULL DEFAULT GETDATE()
    );
END
