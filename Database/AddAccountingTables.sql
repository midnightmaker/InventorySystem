-- Database/AddAccountingTables.sql
-- SQL Migration Script to Add Accounting Tables to Existing Database
-- Run this script against your SQLite database

-- ============= CREATE ACCOUNTS TABLE =============
CREATE TABLE IF NOT EXISTS "Accounts" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY AUTOINCREMENT,
    "AccountCode" TEXT NOT NULL CONSTRAINT "UQ_Accounts_AccountCode" UNIQUE,
    "AccountName" TEXT NOT NULL,
    "Description" TEXT NULL,
    "AccountType" INTEGER NOT NULL,
    "AccountSubType" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "IsSystemAccount" INTEGER NOT NULL DEFAULT 0,
    "CurrentBalance" REAL NOT NULL DEFAULT 0.0,
    "LastTransactionDate" TEXT NOT NULL DEFAULT '1900-01-01',
    "ParentAccountId" INTEGER NULL,
    "CreatedDate" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "CreatedBy" TEXT NULL,
    CONSTRAINT "FK_Accounts_ParentAccount" FOREIGN KEY ("ParentAccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT
);

-- Create indexes for Accounts table
CREATE INDEX IF NOT EXISTS "IX_Accounts_AccountCode" ON "Accounts" ("AccountCode");
CREATE INDEX IF NOT EXISTS "IX_Accounts_AccountType" ON "Accounts" ("AccountType");
CREATE INDEX IF NOT EXISTS "IX_Accounts_ParentAccountId" ON "Accounts" ("ParentAccountId");

-- ============= CREATE GENERAL LEDGER ENTRIES TABLE =============
CREATE TABLE IF NOT EXISTS "GeneralLedgerEntries" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_GeneralLedgerEntries" PRIMARY KEY AUTOINCREMENT,
    "TransactionDate" TEXT NOT NULL,
    "TransactionNumber" TEXT NOT NULL,
    "AccountId" INTEGER NOT NULL,
    "Description" TEXT NOT NULL DEFAULT '',
    "DebitAmount" REAL NOT NULL DEFAULT 0.0,
    "CreditAmount" REAL NOT NULL DEFAULT 0.0,
    "ReferenceType" TEXT NULL,
    "ReferenceId" INTEGER NULL,
    "CreatedBy" TEXT NULL,
    "CreatedDate" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "FK_GeneralLedgerEntries_Account" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT
);

-- Create indexes for GeneralLedgerEntries table
CREATE INDEX IF NOT EXISTS "IX_GeneralLedgerEntries_TransactionDate" ON "GeneralLedgerEntries" ("TransactionDate");
CREATE INDEX IF NOT EXISTS "IX_GeneralLedgerEntries_TransactionNumber" ON "GeneralLedgerEntries" ("TransactionNumber");
CREATE INDEX IF NOT EXISTS "IX_GeneralLedgerEntries_AccountId" ON "GeneralLedgerEntries" ("AccountId");
CREATE INDEX IF NOT EXISTS "IX_GeneralLedgerEntries_Reference" ON "GeneralLedgerEntries" ("ReferenceType", "ReferenceId");

-- ============= CREATE ACCOUNTS PAYABLE TABLE =============
CREATE TABLE IF NOT EXISTS "AccountsPayable" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AccountsPayable" PRIMARY KEY AUTOINCREMENT,
    "VendorId" INTEGER NOT NULL,
    "PurchaseId" INTEGER NOT NULL,
    "InvoiceNumber" TEXT NOT NULL,
    "InvoiceDate" TEXT NOT NULL,
    "DueDate" TEXT NOT NULL,
    "InvoiceAmount" REAL NOT NULL,
    "AmountPaid" REAL NOT NULL DEFAULT 0.0,
    "DiscountTaken" REAL NOT NULL DEFAULT 0.0,
    "PaymentStatus" INTEGER NOT NULL DEFAULT 0,
    "Notes" TEXT NULL,
    "CreatedDate" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "CreatedBy" TEXT NULL,
    "LastModifiedDate" TEXT NULL,
    "LastModifiedBy" TEXT NULL,
    CONSTRAINT "FK_AccountsPayable_Vendor" FOREIGN KEY ("VendorId") REFERENCES "Vendors" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_AccountsPayable_Purchase" FOREIGN KEY ("PurchaseId") REFERENCES "Purchases" ("Id") ON DELETE RESTRICT
);

-- Create indexes for AccountsPayable table
CREATE INDEX IF NOT EXISTS "IX_AccountsPayable_VendorId" ON "AccountsPayable" ("VendorId");
CREATE INDEX IF NOT EXISTS "IX_AccountsPayable_PurchaseId" ON "AccountsPayable" ("PurchaseId");
CREATE INDEX IF NOT EXISTS "IX_AccountsPayable_InvoiceNumber" ON "AccountsPayable" ("InvoiceNumber");
CREATE INDEX IF NOT EXISTS "IX_AccountsPayable_DueDate" ON "AccountsPayable" ("DueDate");
CREATE INDEX IF NOT EXISTS "IX_AccountsPayable_PaymentStatus" ON "AccountsPayable" ("PaymentStatus");

-- ============= CREATE VENDOR PAYMENTS TABLE =============
CREATE TABLE IF NOT EXISTS "VendorPayments" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_VendorPayments" PRIMARY KEY AUTOINCREMENT,
    "AccountsPayableId" INTEGER NOT NULL,
    "PaymentDate" TEXT NOT NULL,
    "PaymentAmount" REAL NOT NULL,
    "DiscountAmount" REAL NOT NULL DEFAULT 0.0,
    "CheckNumber" TEXT NULL,
    "PaymentMethod" INTEGER NOT NULL DEFAULT 1,
    "BankAccount" TEXT NULL,
    "Notes" TEXT NULL,
    "ReferenceNumber" TEXT NULL,
    "CreatedBy" TEXT NULL,
    "CreatedDate" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "FK_VendorPayments_AccountsPayable" FOREIGN KEY ("AccountsPayableId") REFERENCES "AccountsPayable" ("Id") ON DELETE CASCADE
);

-- Create indexes for VendorPayments table
CREATE INDEX IF NOT EXISTS "IX_VendorPayments_AccountsPayableId" ON "VendorPayments" ("AccountsPayableId");
CREATE INDEX IF NOT EXISTS "IX_VendorPayments_PaymentDate" ON "VendorPayments" ("PaymentDate");
CREATE INDEX IF NOT EXISTS "IX_VendorPayments_CheckNumber" ON "VendorPayments" ("CheckNumber");

-- ============= ADD ACCOUNTING COLUMNS TO EXISTING TABLES =============

-- Add accounting columns to Purchases table
ALTER TABLE "Purchases" ADD COLUMN "AccountCode" TEXT NULL;
ALTER TABLE "Purchases" ADD COLUMN "JournalEntryNumber" TEXT NULL;
ALTER TABLE "Purchases" ADD COLUMN "IsJournalEntryGenerated" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "Purchases" ADD COLUMN "JournalEntryGeneratedDate" TEXT NULL;
ALTER TABLE "Purchases" ADD COLUMN "JournalEntryGeneratedBy" TEXT NULL;

-- Add accounting columns to Sales table
ALTER TABLE "Sales" ADD COLUMN "RevenueAccountCode" TEXT NULL DEFAULT '4000';
ALTER TABLE "Sales" ADD COLUMN "JournalEntryNumber" TEXT NULL;
ALTER TABLE "Sales" ADD COLUMN "IsJournalEntryGenerated" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "Sales" ADD COLUMN "JournalEntryGeneratedDate" TEXT NULL;
ALTER TABLE "Sales" ADD COLUMN "JournalEntryGeneratedBy" TEXT NULL;

-- ============= INSERT DEFAULT CHART OF ACCOUNTS =============

-- Assets
INSERT OR IGNORE INTO "Accounts" ("AccountCode", "AccountName", "Description", "AccountType", "AccountSubType", "IsActive", "IsSystemAccount", "CreatedBy") VALUES
('1000', 'Cash - Operating', 'Primary business checking account', 1, 101, 1, 1, 'System'),
('1100', 'Accounts Receivable', 'Money owed by customers', 1, 101, 1, 1, 'System'),
('1200', 'Raw Materials Inventory', 'Cost of raw materials on hand', 1, 103, 1, 1, 'System'),
('1210', 'Work in Process Inventory', 'Cost of partially completed products', 1, 103, 1, 1, 'System'),
('1220', 'Finished Goods Inventory', 'Cost of completed products ready for sale', 1, 103, 1, 1, 'System'),
('1230', 'Supplies Inventory', 'Office and manufacturing supplies', 1, 103, 1, 1, 'System'),
('1240', 'R&D Materials Inventory', 'Research and development materials', 1, 103, 1, 1, 'System'),
('1600', 'Manufacturing Equipment', 'Cost of production machinery and equipment', 1, 102, 1, 1, 'System'),
('1601', 'Accumulated Depreciation - Manufacturing Equipment', 'Total depreciation on manufacturing equipment', 1, 102, 1, 1, 'System'),
('1700', 'Office Equipment', 'Cost of office furniture and equipment', 1, 102, 1, 0, 'System'),
('1900', 'Software & Licenses', 'Cost of software and licensing', 1, 102, 1, 1, 'System');

-- Liabilities
INSERT OR IGNORE INTO "Accounts" ("AccountCode", "AccountName", "Description", "AccountType", "AccountSubType", "IsActive", "IsSystemAccount", "CreatedBy") VALUES
('2000', 'Accounts Payable', 'Money owed to vendors and suppliers', 2, 201, 1, 1, 'System'),
('2100', 'Accrued Payroll', 'Unpaid wages and salaries', 2, 201, 1, 0, 'System'),
('2200', 'Accrued Expenses', 'Expenses incurred but not yet paid', 2, 201, 1, 1, 'System');

-- Equity
INSERT OR IGNORE INTO "Accounts" ("AccountCode", "AccountName", "Description", "AccountType", "AccountSubType", "IsActive", "IsSystemAccount", "CreatedBy") VALUES
('3000', 'Owner''s Equity', 'Owner''s investment in the business', 3, 301, 1, 1, 'System'),
('3100', 'Retained Earnings', 'Accumulated profits retained in business', 3, 302, 1, 1, 'System');

-- Revenue
INSERT OR IGNORE INTO "Accounts" ("AccountCode", "AccountName", "Description", "AccountType", "AccountSubType", "IsActive", "IsSystemAccount", "CreatedBy") VALUES
('4000', 'Product Sales', 'Revenue from manufactured products', 4, 401, 1, 1, 'System'),
('4100', 'Service Revenue', 'Revenue from services provided', 4, 402, 1, 1, 'System'),
('4200', 'Custom Manufacturing', 'Revenue from custom manufacturing jobs', 4, 401, 1, 1, 'System');

-- Cost of Goods Sold
INSERT OR IGNORE INTO "Accounts" ("AccountCode", "AccountName", "Description", "AccountType", "AccountSubType", "IsActive", "IsSystemAccount", "CreatedBy") VALUES
('5000', 'Cost of Goods Sold', 'Direct cost of products sold', 5, 501, 1, 1, 'System'),
('5100', 'Raw Materials Used', 'Cost of raw materials consumed in production', 5, 501, 1, 1, 'System'),
('5200', 'Direct Labor', 'Labor directly involved in production', 5, 501, 1, 1, 'System'),
('5300', 'Manufacturing Overhead', 'Indirect manufacturing costs', 5, 501, 1, 1, 'System'),
('5400', 'R&D Materials', 'Research and development material costs', 5, 501, 1, 1, 'System');

-- Operating Expenses
INSERT OR IGNORE INTO "Accounts" ("AccountCode", "AccountName", "Description", "AccountType", "AccountSubType", "IsActive", "IsSystemAccount", "CreatedBy") VALUES
('6000', 'General Operating Expenses', 'Miscellaneous operating expenses', 5, 502, 1, 1, 'System'),
('6210', 'Electricity', 'Electric utility expenses', 5, 503, 1, 1, 'System'),
('6220', 'Gas & Heating', 'Natural gas and heating expenses', 5, 503, 1, 1, 'System'),
('6230', 'Water & Sewer', 'Water and sewer utility expenses', 5, 503, 1, 1, 'System'),
('6240', 'Internet & Phone', 'Telecommunications expenses', 5, 503, 1, 1, 'System'),
('6300', 'Software Subscriptions', 'Monthly/annual software licensing', 5, 504, 1, 1, 'System'),
('6310', 'Cloud Services', 'AWS, Azure, Google Cloud expenses', 5, 504, 1, 1, 'System'),
('6700', 'Office Supplies', 'Paper, pens, general office supplies', 5, 502, 1, 1, 'System'),
('6710', 'Manufacturing Supplies', 'Shop supplies, tools, consumables', 5, 502, 1, 1, 'System');

-- ============= UPDATE EXISTING PURCHASES WITH ACCOUNT CODES =============

-- Update purchases with appropriate account codes based on item type
UPDATE "Purchases" SET "AccountCode" = '1200' -- Raw Materials Inventory
WHERE "AccountCode" IS NULL 
AND EXISTS (
    SELECT 1 FROM "Items" 
    WHERE "Items"."Id" = "Purchases"."ItemId" 
    AND "Items"."ItemType" = 0 -- Inventoried
    AND ("Items"."MaterialType" = 1 OR "Items"."MaterialType" = 0 OR "Items"."MaterialType" IS NULL) -- RawMaterial, Standard, or NULL
);

UPDATE "Purchases" SET "AccountCode" = '1220' -- Finished Goods Inventory
WHERE "AccountCode" IS NULL 
AND EXISTS (
    SELECT 1 FROM "Items" 
    WHERE "Items"."Id" = "Purchases"."ItemId" 
    AND "Items"."ItemType" = 0 -- Inventoried
    AND "Items"."MaterialType" = 2 -- Transformed
);

UPDATE "Purchases" SET "AccountCode" = '1210' -- Work in Process Inventory
WHERE "AccountCode" IS NULL 
AND EXISTS (
    SELECT 1 FROM "Items" 
    WHERE "Items"."Id" = "Purchases"."ItemId" 
    AND "Items"."ItemType" = 0 -- Inventoried
    AND "Items"."MaterialType" = 3 -- WorkInProcess
);

UPDATE "Purchases" SET "AccountCode" = '5100' -- Raw Materials Used (expense)
WHERE "AccountCode" IS NULL 
AND EXISTS (
    SELECT 1 FROM "Items" 
    WHERE "Items"."Id" = "Purchases"."ItemId" 
    AND "Items"."ItemType" = 1 -- NonInventoried
);

UPDATE "Purchases" SET "AccountCode" = '6710' -- Manufacturing Supplies
WHERE "AccountCode" IS NULL 
AND EXISTS (
    SELECT 1 FROM "Items" 
    WHERE "Items"."Id" = "Purchases"."ItemId" 
    AND "Items"."ItemType" = 4 -- Consumable
);

UPDATE "Purchases" SET "AccountCode" = '6210' -- Utilities
WHERE "AccountCode" IS NULL 
AND EXISTS (
    SELECT 1 FROM "Items" 
    WHERE "Items"."Id" = "Purchases"."ItemId" 
    AND "Items"."ItemType" = 7 -- Utility
);

UPDATE "Purchases" SET "AccountCode" = '6300' -- Software Subscriptions
WHERE "AccountCode" IS NULL 
AND EXISTS (
    SELECT 1 FROM "Items" 
    WHERE "Items"."Id" = "Purchases"."ItemId" 
    AND "Items"."ItemType" = 6 -- Subscription
);

UPDATE "Purchases" SET "AccountCode" = '5400' -- R&D Materials
WHERE "AccountCode" IS NULL 
AND EXISTS (
    SELECT 1 FROM "Items" 
    WHERE "Items"."Id" = "Purchases"."ItemId" 
    AND "Items"."ItemType" = 8 -- RnDMaterials
);

UPDATE "Purchases" SET "AccountCode" = '6000' -- General Operating Expenses (fallback)
WHERE "AccountCode" IS NULL;

-- ============= VERIFICATION QUERIES =============

-- Verify tables were created
SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Accounts', 'GeneralLedgerEntries', 'AccountsPayable', 'VendorPayments');

-- Count default accounts
SELECT COUNT(*) as AccountCount FROM Accounts;

-- Show account breakdown by type
SELECT AccountType, COUNT(*) as Count FROM Accounts GROUP BY AccountType;

-- Verify purchases have account codes assigned
SELECT 
    AccountCode, 
    COUNT(*) as PurchaseCount 
FROM Purchases 
WHERE AccountCode IS NOT NULL 
GROUP BY AccountCode 
ORDER BY AccountCode;

-- Show any purchases without account codes (should be 0)
SELECT COUNT(*) as PurchasesWithoutAccountCode FROM Purchases WHERE AccountCode IS NULL;