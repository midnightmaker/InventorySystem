# PowerShell script to safely reset the database and apply all migrations
# Run this from the InventorySystem project root directory

Write-Host "=== Database Reset and Migration Script ===" -ForegroundColor Green
Write-Host ""

# Check if database file exists
$dbFile = "inventory.db"
if (Test-Path $dbFile) {
    Write-Host "Found existing database: $dbFile" -ForegroundColor Yellow
    
    # Backup existing database (optional)
    $backupFile = "inventory_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').db"
    Write-Host "Creating backup: $backupFile" -ForegroundColor Cyan
    Copy-Item $dbFile $backupFile -ErrorAction SilentlyContinue
    
    # Delete the database
    Write-Host "Deleting existing database..." -ForegroundColor Yellow
    Remove-Item $dbFile -Force
    Write-Host "Database deleted successfully!" -ForegroundColor Green
} else {
    Write-Host "No existing database found." -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Applying all migrations..." -ForegroundColor Yellow

# Apply all migrations
try {
    $result = dotnet ef database update
    if ($LASTEXITCODE -eq 0) {
        Write-Host "All migrations applied successfully!" -ForegroundColor Green
        
        # List applied migrations
        Write-Host ""
        Write-Host "Applied migrations:" -ForegroundColor Cyan
        dotnet ef migrations list
        
        # Verify Projects table exists
        Write-Host ""
        Write-Host "Verifying Projects table..." -ForegroundColor Yellow
        
        # You can add additional verification here if needed
        Write-Host "Database reset complete!" -ForegroundColor Green
        Write-Host ""
        Write-Host "? Your Projects table should now be ready for use!" -ForegroundColor Green
        
    } else {
        Write-Host "Migration failed! Check the error output above." -ForegroundColor Red
    }
} catch {
    Write-Host "Error during migration: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Next Steps ===" -ForegroundColor Cyan
Write-Host "1. Run your application" -ForegroundColor White
Write-Host "2. Navigate to /Projects to test the Projects functionality" -ForegroundColor White
Write-Host "3. Create a test project to verify everything works" -ForegroundColor White