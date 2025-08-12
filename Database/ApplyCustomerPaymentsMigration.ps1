# Apply CustomerPayments table migration
Write-Host "Creating CustomerPayments table..." -ForegroundColor Green

$sqlFile = "Database\CreateCustomerPaymentsTable.sql"
$dbPath = "inventory.db"

if (-not (Test-Path $sqlFile)) {
    Write-Error "SQL file not found: $sqlFile"
    exit 1
}

if (-not (Test-Path $dbPath)) {
    Write-Error "Database file not found: $dbPath"
    exit 1
}

try {
    # Read the SQL script
    $sql = Get-Content $sqlFile -Raw
    
    # Execute the SQL script using SQLite
    sqlite3 $dbPath $sql
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "CustomerPayments table created successfully!" -ForegroundColor Green
        
        # Verify the table was created
        $tableCheck = sqlite3 $dbPath "SELECT name FROM sqlite_master WHERE type='table' AND name='CustomerPayments';"
        if ($tableCheck -eq "CustomerPayments") {
            Write-Host "Table verification successful." -ForegroundColor Green
        } else {
            Write-Warning "Table verification failed."
        }
        
        # Show table structure
        Write-Host "`nTable structure:" -ForegroundColor Yellow
        sqlite3 $dbPath ".schema CustomerPayments"
        
        # Show indexes
        Write-Host "`nIndexes created:" -ForegroundColor Yellow
        sqlite3 $dbPath "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='CustomerPayments';"
    } else {
        Write-Error "Failed to create CustomerPayments table. Exit code: $LASTEXITCODE"
        exit 1
    }
} catch {
    Write-Error "Error applying migration: $_"
    exit 1
}

Write-Host "`nMigration completed successfully!" -ForegroundColor Green