-- Create CustomerPayments table
CREATE TABLE CustomerPayments (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SaleId INTEGER NOT NULL,
    CustomerId INTEGER NOT NULL,
    PaymentDate TEXT NOT NULL, -- SQLite stores dates as TEXT
    Amount DECIMAL(18,2) NOT NULL,
    PaymentMethod TEXT NOT NULL,
    PaymentReference TEXT NULL,
    Notes TEXT NULL,
    Status INTEGER NOT NULL DEFAULT 1, -- PaymentRecordStatus enum
    CreatedDate TEXT NOT NULL DEFAULT (datetime('now')),
    CreatedBy TEXT NOT NULL,
    LastUpdated TEXT NULL,
    UpdatedBy TEXT NULL,
    
    -- Foreign key constraints
    FOREIGN KEY (SaleId) REFERENCES Sales(Id) ON DELETE CASCADE,
    FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE CASCADE
);

-- Create indexes for better query performance
CREATE INDEX IX_CustomerPayments_SaleId ON CustomerPayments(SaleId);
CREATE INDEX IX_CustomerPayments_CustomerId ON CustomerPayments(CustomerId);
CREATE INDEX IX_CustomerPayments_PaymentDate ON CustomerPayments(PaymentDate);
CREATE INDEX IX_CustomerPayments_PaymentMethod ON CustomerPayments(PaymentMethod);
CREATE INDEX IX_CustomerPayments_Status ON CustomerPayments(Status);

-- Create composite index for common queries
CREATE INDEX IX_CustomerPayments_Customer_Date ON CustomerPayments(CustomerId, PaymentDate);
CREATE INDEX IX_CustomerPayments_Sale_Status ON CustomerPayments(SaleId, Status);

PRAGMA foreign_keys = ON;