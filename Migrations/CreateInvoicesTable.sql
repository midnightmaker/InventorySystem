-- Migration: Create Invoices table
-- Run this against your database when upgrading to a version that includes
-- the Invoice model (frozen "as-invoiced" billing events).
--
-- Each shipment confirmation creates one Invoice row.
-- The PdfData column stores the rendered wkhtmltopdf bytes as an immutable snapshot.
-- Once written, PdfData must never be updated.

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'dbo.Invoices') AND type = N'U'
)
BEGIN
    CREATE TABLE dbo.Invoices
    (
        Id              int             NOT NULL IDENTITY(1,1),
        InvoiceNumber   nvarchar(50)    NOT NULL,
        InvoiceType     int             NOT NULL DEFAULT 0,   -- 0=Invoice 1=CreditMemo 2=Adjustment
        InvoiceDate     date            NOT NULL,
        DueDate         date            NULL,

        -- Foreign keys
        SaleId          int             NOT NULL,
        ShipmentId      int             NULL,

        -- Snapshot amounts (frozen at time of invoicing — do not update)
        SubtotalAmount  decimal(18,2)   NOT NULL DEFAULT 0,
        DiscountAmount  decimal(18,2)   NOT NULL DEFAULT 0,
        TaxAmount       decimal(18,2)   NOT NULL DEFAULT 0,
        ShippingAmount  decimal(18,2)   NOT NULL DEFAULT 0,
        TotalAmount     decimal(18,2)   NOT NULL DEFAULT 0,

        -- Frozen PDF document
        PdfData         varbinary(max)  NULL,
        PdfGeneratedAt  datetime2       NULL,

        -- Audit
        IssuedBy        nvarchar(100)   NULL,
        Notes           nvarchar(1000)  NULL,
        CreatedAt       datetime2       NOT NULL DEFAULT GETDATE(),

        CONSTRAINT PK_Invoices PRIMARY KEY (Id),
        CONSTRAINT UQ_Invoices_InvoiceNumber UNIQUE (InvoiceNumber),

        CONSTRAINT FK_Invoices_Sales
            FOREIGN KEY (SaleId) REFERENCES dbo.Sales(Id)
            ON DELETE NO ACTION,

        CONSTRAINT FK_Invoices_Shipments
            FOREIGN KEY (ShipmentId) REFERENCES dbo.Shipments(Id)
            ON DELETE SET NULL
    );

    -- Performance indexes
    CREATE INDEX IX_Invoices_SaleId      ON dbo.Invoices (SaleId);
    CREATE INDEX IX_Invoices_ShipmentId  ON dbo.Invoices (ShipmentId);
    CREATE INDEX IX_Invoices_InvoiceDate ON dbo.Invoices (InvoiceDate);
    CREATE INDEX IX_Invoices_InvoiceType ON dbo.Invoices (InvoiceType);

    PRINT 'Table dbo.Invoices created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.Invoices already exists — skipped.';
END

PRINT 'CreateInvoicesTable migration complete.';
