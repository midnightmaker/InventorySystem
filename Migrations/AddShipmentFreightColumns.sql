-- Migration: Add freight/carrier tracking columns to Shipments table
-- Run this against your database when upgrading from a version that did not
-- have ShippingAccountType, ActualCarrierCost, or FreightOutExpensePaymentId.

-- ShippingAccountType (int, NOT NULL, default 0 = OurAccount)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Shipments')
      AND name = N'ShippingAccountType'
)
BEGIN
    ALTER TABLE dbo.Shipments
        ADD ShippingAccountType int NOT NULL DEFAULT 0;

    PRINT 'Column ShippingAccountType added to Shipments.';
END
ELSE
BEGIN
    PRINT 'Column ShippingAccountType already exists in Shipments — skipped.';
END

-- ActualCarrierCost (decimal(18,2), NULL)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Shipments')
      AND name = N'ActualCarrierCost'
)
BEGIN
    ALTER TABLE dbo.Shipments
        ADD ActualCarrierCost decimal(18,2) NULL;

    PRINT 'Column ActualCarrierCost added to Shipments.';
END
ELSE
BEGIN
    PRINT 'Column ActualCarrierCost already exists in Shipments — skipped.';
END

-- FreightOutExpensePaymentId (int, NULL, no FK enforced here — EF manages the relationship)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Shipments')
      AND name = N'FreightOutExpensePaymentId'
)
BEGIN
    ALTER TABLE dbo.Shipments
        ADD FreightOutExpensePaymentId int NULL;

    PRINT 'Column FreightOutExpensePaymentId added to Shipments.';
END
ELSE
BEGIN
    PRINT 'Column FreightOutExpensePaymentId already exists in Shipments — skipped.';
END

PRINT 'AddShipmentFreightColumns migration complete.';
