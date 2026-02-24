-- Migration: Remove IsQuotation column from Sales table
-- IsQuotation is now a computed property derived from SaleStatus == 0 (Quotation).
-- Before dropping the column, fix any data inconsistencies:
--   • Rows where IsQuotation = 1 but SaleStatus <> 0  ? set SaleStatus = 0 (Quotation)
--   • Rows where IsQuotation = 0 but SaleStatus = 0   ? leave as-is (SaleStatus already correct)

-- Step 1: Repair inconsistent rows — ensure every "IsQuotation = 1" record has SaleStatus = Quotation (0)
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Sales')
      AND name = N'IsQuotation'
)
BEGIN
    -- Set SaleStatus = 0 (Quotation) for any row that was flagged as a quotation
    -- but whose SaleStatus was left at a non-quotation value.
    UPDATE dbo.Sales
    SET SaleStatus = 0   -- SaleStatus.Quotation
    WHERE IsQuotation = 1
      AND SaleStatus <> 0;

    PRINT 'Data repair: aligned SaleStatus for mismatched IsQuotation rows.';

    -- Step 2: Drop the now-redundant column
    ALTER TABLE dbo.Sales DROP COLUMN IsQuotation;

    PRINT 'Column IsQuotation dropped from Sales.';
END
ELSE
BEGIN
    PRINT 'Column IsQuotation does not exist in Sales — skipped.';
END

PRINT 'RemoveIsQuotationFromSales migration complete.';
