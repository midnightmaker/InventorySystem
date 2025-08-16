-- Migration script to add Service module tables
-- Run this against your database to add the service functionality

-- Create ServiceTypes table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ServiceTypes' AND xtype='U')
BEGIN
    CREATE TABLE ServiceTypes (
        Id int IDENTITY(1,1) PRIMARY KEY,
        ServiceName nvarchar(100) NOT NULL,
        ServiceCategory nvarchar(50) NULL,
        Description nvarchar(500) NULL,
        StandardHours decimal(5,2) NOT NULL DEFAULT 0,
        StandardRate decimal(18,2) NOT NULL DEFAULT 0,
        RequiresEquipment bit NOT NULL DEFAULT 0,
        RequiredEquipment nvarchar(200) NULL,
        SkillLevel nvarchar(100) NULL,
        QcRequired bit NOT NULL DEFAULT 0,
        CertificateRequired bit NOT NULL DEFAULT 0,
        IsActive bit NOT NULL DEFAULT 1,
        ServiceCode nvarchar(20) NULL,
        CONSTRAINT UQ_ServiceTypes_ServiceCode UNIQUE (ServiceCode)
    );
END

-- Create ServiceOrders table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ServiceOrders' AND xtype='U')
BEGIN
    CREATE TABLE ServiceOrders (
        Id int IDENTITY(1,1) PRIMARY KEY,
        ServiceOrderNumber nvarchar(50) NOT NULL,
        CustomerId int NOT NULL,
        SaleId int NULL,
        ServiceTypeId int NOT NULL,
        RequestDate datetime2(7) NOT NULL DEFAULT GETDATE(),
        PromisedDate datetime2(7) NULL,
        ScheduledDate datetime2(7) NULL,
        StartedDate datetime2(7) NULL,
        CompletedDate datetime2(7) NULL,
        Status int NOT NULL DEFAULT 0,
        Priority int NOT NULL DEFAULT 1,
        EstimatedHours decimal(5,2) NOT NULL DEFAULT 0,
        ActualHours decimal(5,2) NOT NULL DEFAULT 0,
        EstimatedCost decimal(18,2) NOT NULL DEFAULT 0,
        ActualCost decimal(18,2) NOT NULL DEFAULT 0,
        CustomerRequest nvarchar(1000) NULL,
        ServiceNotes nvarchar(2000) NULL,
        InternalNotes nvarchar(2000) NULL,
        EquipmentDetails nvarchar(200) NULL,
        SerialNumber nvarchar(100) NULL,
        ModelNumber nvarchar(100) NULL,
        AssignedTechnician nvarchar(100) NULL,
        WorkLocation nvarchar(100) NULL,
        IsPrepaid bit NOT NULL DEFAULT 0,
        PaymentMethod nvarchar(50) NULL,
        IsBillable bit NOT NULL DEFAULT 1,
        HourlyRate decimal(18,2) NOT NULL DEFAULT 0,
        QcRequired bit NOT NULL DEFAULT 0,
        QcCompleted bit NOT NULL DEFAULT 0,
        QcDate datetime2(7) NULL,
        QcTechnician nvarchar(100) NULL,
        QcNotes nvarchar(1000) NULL,
        CertificateRequired bit NOT NULL DEFAULT 0,
        CertificateGenerated bit NOT NULL DEFAULT 0,
        CertificateNumber nvarchar(50) NULL,
        CreatedDate datetime2(7) NOT NULL DEFAULT GETDATE(),
        LastModifiedDate datetime2(7) NULL,
        CreatedBy nvarchar(100) NULL,
        LastModifiedBy nvarchar(100) NULL,
        CONSTRAINT UQ_ServiceOrders_ServiceOrderNumber UNIQUE (ServiceOrderNumber),
        CONSTRAINT FK_ServiceOrders_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
        CONSTRAINT FK_ServiceOrders_Sales FOREIGN KEY (SaleId) REFERENCES Sales(Id),
        CONSTRAINT FK_ServiceOrders_ServiceTypes FOREIGN KEY (ServiceTypeId) REFERENCES ServiceTypes(Id)
    );
END

-- Create ServiceTimeLogs table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ServiceTimeLogs' AND xtype='U')
BEGIN
    CREATE TABLE ServiceTimeLogs (
        Id int IDENTITY(1,1) PRIMARY KEY,
        ServiceOrderId int NOT NULL,
        Date datetime2(7) NOT NULL DEFAULT GETDATE(),
        Technician nvarchar(100) NOT NULL,
        Hours decimal(5,2) NOT NULL,
        HourlyRate decimal(18,2) NOT NULL,
        WorkDescription nvarchar(1000) NULL,
        IsBillable bit NOT NULL DEFAULT 1,
        CreatedDate datetime2(7) NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_ServiceTimeLogs_ServiceOrders FOREIGN KEY (ServiceOrderId) REFERENCES ServiceOrders(Id) ON DELETE CASCADE
    );
END

-- Create ServiceMaterials table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ServiceMaterials' AND xtype='U')
BEGIN
    CREATE TABLE ServiceMaterials (
        Id int IDENTITY(1,1) PRIMARY KEY,
        ServiceOrderId int NOT NULL,
        ItemId int NOT NULL,
        QuantityUsed decimal(18,4) NOT NULL,
        UnitCost decimal(18,6) NOT NULL,
        IsBillable bit NOT NULL DEFAULT 1,
        Notes nvarchar(500) NULL,
        UsedDate datetime2(7) NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_ServiceMaterials_ServiceOrders FOREIGN KEY (ServiceOrderId) REFERENCES ServiceOrders(Id) ON DELETE CASCADE,
        CONSTRAINT FK_ServiceMaterials_Items FOREIGN KEY (ItemId) REFERENCES Items(Id)
    );
END

-- Create ServiceDocuments table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ServiceDocuments' AND xtype='U')
BEGIN
    CREATE TABLE ServiceDocuments (
        Id int IDENTITY(1,1) PRIMARY KEY,
        ServiceOrderId int NOT NULL,
        DocumentName nvarchar(200) NOT NULL,
        OriginalFileName nvarchar(200) NULL,
        ContentType nvarchar(100) NULL,
        FileSize bigint NOT NULL DEFAULT 0,
        DocumentData varbinary(max) NULL,
        DocumentType nvarchar(50) NOT NULL DEFAULT 'General',
        Description nvarchar(500) NULL,
        UploadedDate datetime2(7) NOT NULL DEFAULT GETDATE(),
        UploadedBy nvarchar(100) NULL,
        CONSTRAINT FK_ServiceDocuments_ServiceOrders FOREIGN KEY (ServiceOrderId) REFERENCES ServiceOrders(Id) ON DELETE CASCADE
    );
END

-- Create indexes for performance
CREATE NONCLUSTERED INDEX IX_ServiceOrders_CustomerId ON ServiceOrders(CustomerId);
CREATE NONCLUSTERED INDEX IX_ServiceOrders_ServiceTypeId ON ServiceOrders(ServiceTypeId);
CREATE NONCLUSTERED INDEX IX_ServiceOrders_Status ON ServiceOrders(Status);
CREATE NONCLUSTERED INDEX IX_ServiceOrders_RequestDate ON ServiceOrders(RequestDate);
CREATE NONCLUSTERED INDEX IX_ServiceOrders_ScheduledDate ON ServiceOrders(ScheduledDate) WHERE ScheduledDate IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_ServiceTimeLogs_ServiceOrderId ON ServiceTimeLogs(ServiceOrderId);
CREATE NONCLUSTERED INDEX IX_ServiceMaterials_ServiceOrderId ON ServiceMaterials(ServiceOrderId);
CREATE NONCLUSTERED INDEX IX_ServiceDocuments_ServiceOrderId ON ServiceDocuments(ServiceOrderId);

-- Insert default service types
INSERT INTO ServiceTypes (ServiceName, ServiceCategory, Description, StandardHours, StandardRate, ServiceCode, QcRequired, CertificateRequired, IsActive)
VALUES 
    ('Equipment Calibration', 'Calibration', 'Standard equipment calibration service', 2.0, 125.00, 'CAL001', 1, 1, 1),
    ('Preventive Maintenance', 'Maintenance', 'Scheduled preventive maintenance service', 1.5, 95.00, 'MAINT001', 1, 0, 1),
    ('Equipment Repair', 'Repair', 'General equipment repair service', 3.0, 110.00, 'REP001', 1, 0, 1),
    ('Installation Service', 'Installation', 'Equipment installation and setup', 4.0, 100.00, 'INST001', 1, 0, 1),
    ('Training Service', 'Training', 'Equipment operation training', 6.0, 85.00, 'TRAIN001', 0, 1, 1),
    ('Consultation', 'Consultation', 'Technical consultation service', 1.0, 150.00, 'CONS001', 0, 0, 1);

PRINT 'Service module tables created successfully!';