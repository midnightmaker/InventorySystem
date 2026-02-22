-- Migration script to add AuditLogs table
-- Run this against your database to add the audit trail functionality

-- Create AuditLogs table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditLogs' AND xtype='U')
BEGIN
    CREATE TABLE AuditLogs (
        Id int IDENTITY(1,1) PRIMARY KEY,
        EntityName nvarchar(100) NOT NULL,
        EntityId nvarchar(50) NOT NULL DEFAULT '',
        Action nvarchar(20) NOT NULL,
        OldValues nvarchar(max) NULL,
        NewValues nvarchar(max) NULL,
        AffectedColumns nvarchar(2000) NULL,
        PerformedBy nvarchar(100) NULL,
        Timestamp datetime2(7) NOT NULL DEFAULT GETUTCDATE(),
        Summary nvarchar(500) NULL
    );

    PRINT 'AuditLogs table created successfully.';
END
ELSE
BEGIN
    PRINT 'AuditLogs table already exists, skipping creation.';
END

-- Create indexes for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLogs_Timestamp' AND object_id = OBJECT_ID('AuditLogs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Timestamp
        ON AuditLogs(Timestamp DESC);
    PRINT 'Index IX_AuditLogs_Timestamp created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLogs_EntityName_EntityId' AND object_id = OBJECT_ID('AuditLogs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLogs_EntityName_EntityId
        ON AuditLogs(EntityName, EntityId);
    PRINT 'Index IX_AuditLogs_EntityName_EntityId created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLogs_Action' AND object_id = OBJECT_ID('AuditLogs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Action
        ON AuditLogs(Action);
    PRINT 'Index IX_AuditLogs_Action created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLogs_PerformedBy' AND object_id = OBJECT_ID('AuditLogs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLogs_PerformedBy
        ON AuditLogs(PerformedBy)
        WHERE PerformedBy IS NOT NULL;
    PRINT 'Index IX_AuditLogs_PerformedBy created.';
END

PRINT 'AuditLogs migration completed successfully!';
