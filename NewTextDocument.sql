-- Add foreign key constraint
ALTER TABLE Expenses 
ADD CONSTRAINT FK_Expenses_Accounts_LedgerAccountId 
FOREIGN KEY (LedgerAccountId) REFERENCES Accounts(Id);

-- Update existing expenses with suggested accounts based on category
UPDATE Expenses 
SET LedgerAccountId = (
    SELECT Id FROM Accounts WHERE AccountCode = 
    CASE Category
        WHEN 0 THEN '6100'  -- OfficeSupplies
        WHEN 1 THEN '6200'  -- Utilities  
        WHEN 2 THEN '6300'  -- ProfessionalServices
        WHEN 3 THEN '6400'  -- SoftwareLicenses
        WHEN 4 THEN '6500'  -- Travel
        WHEN 5 THEN '6600'  -- Equipment
        WHEN 6 THEN '6700'  -- Marketing
        WHEN 7 THEN '6800'  -- Research
        WHEN 8 THEN '6900'  -- Insurance
        ELSE '6000'         -- GeneralBusiness (default)
    END
) 
WHERE LedgerAccountId = 6000;