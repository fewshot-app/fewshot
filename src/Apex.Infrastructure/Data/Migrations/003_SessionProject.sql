-- Add Project column to Sessions
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sessions') AND name = 'Project')
BEGIN
    ALTER TABLE Sessions ADD Project NVARCHAR(100) NULL;
END
