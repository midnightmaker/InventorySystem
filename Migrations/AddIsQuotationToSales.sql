-- Migration: Add IsQuotation column to Sales table
-- This column indicates whether a sale originated as a quotation.
-- When true, invoice reports show "QUOTATION" instead of "PROFORMA INVOICE".

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Sales')
      AND name = N'IsQuotation'
)
BEGIN
    ALTER TABLE dbo.Sales
        ADD IsQuotation bit NOT NULL DEFAULT 0;

    PRINT 'Column IsQuotation added to Sales.';
END
ELSE
BEGIN
    PRINT 'Column IsQuotation already exists in Sales – skipped.';
END

PRINT 'AddIsQuotationToSales migration complete.';
