-- Manual Projects Table Creation Script
-- Run this SQL script directly against your SQLite database if EF migrations fail

-- 1. Create the Projects table
CREATE TABLE IF NOT EXISTS "Projects" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Projects" PRIMARY KEY AUTOINCREMENT,
    "ProjectCode" TEXT NOT NULL CONSTRAINT "AK_Projects_ProjectCode" UNIQUE,
    "ProjectName" TEXT NOT NULL,
    "Description" TEXT NULL,
    "ProjectType" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "StartDate" TEXT NULL,
    "ExpectedEndDate" TEXT NULL,
    "ActualEndDate" TEXT NULL,
    "Budget" REAL NOT NULL,
    "ProjectManager" TEXT NULL,
    "Department" TEXT NULL,
    "Priority" INTEGER NOT NULL,
    "Notes" TEXT NULL,
    "CreatedDate" TEXT NOT NULL,
    "CreatedBy" TEXT NULL,
    "LastModifiedDate" TEXT NULL,
    "LastModifiedBy" TEXT NULL
);

-- 2. Add ProjectId column to Purchases table (if it doesn't exist)
-- Check if column exists first
PRAGMA table_info(Purchases);

-- Add ProjectId column (this will fail silently if column already exists)
ALTER TABLE "Purchases" ADD COLUMN "ProjectId" INTEGER NULL;

-- 3. Create indexes
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Projects_ProjectCode" ON "Projects" ("ProjectCode");
CREATE INDEX IF NOT EXISTS "IX_Purchases_ProjectId" ON "Purchases" ("ProjectId");

-- 4. Create foreign key relationship (SQLite doesn't support adding FK constraints to existing tables)
-- The FK constraint will be enforced by EF Core in the application layer

-- 5. Insert into migrations history to mark this migration as applied
INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250811121700_AddProjectsTable', '9.0.7');

-- 6. Verify the table was created successfully
SELECT name FROM sqlite_master WHERE type='table' AND name='Projects';

-- 7. Insert sample project data (optional)
INSERT OR IGNORE INTO "Projects" (
    "ProjectCode", 
    "ProjectName", 
    "Description", 
    "ProjectType", 
    "Status", 
    "Budget", 
    "Priority", 
    "CreatedDate",
    "CreatedBy"
) VALUES (
    'SAMPLE-2025-001',
    'Sample R&D Project',
    'This is a sample project to demonstrate the Projects table functionality',
    0, -- Research
    0, -- Planning
    10000.00,
    1, -- Medium
    datetime('now'),
    'System'
);

-- Verify sample data
SELECT * FROM "Projects" WHERE "ProjectCode" = 'SAMPLE-2025-001';