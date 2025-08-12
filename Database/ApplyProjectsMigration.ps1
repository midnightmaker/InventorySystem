# PowerShell script to apply Projects table creation manually
# Run this from the InventorySystem project root directory

Write-Host "Applying Projects table creation manually..." -ForegroundColor Green

# Check if SQLite CLI is available
$sqliteAvailable = Get-Command sqlite3 -ErrorAction SilentlyContinue

if ($sqliteAvailable) {
    Write-Host "Using SQLite CLI..." -ForegroundColor Yellow
    
    # Run the SQL script using SQLite CLI
    sqlite3 inventory.db ".read Database\CreateProjectsTable.sql"
    
    Write-Host "Projects table creation completed!" -ForegroundColor Green
    
    # Verify the table exists
    Write-Host "Verifying Projects table..." -ForegroundColor Yellow
    sqlite3 inventory.db "SELECT name FROM sqlite_master WHERE type='table' AND name='Projects';"
    
} else {
    Write-Host "SQLite CLI not found. Please run the SQL script manually." -ForegroundColor Red
    Write-Host "Script location: Database\CreateProjectsTable.sql" -ForegroundColor Yellow
    Write-Host "You can:" -ForegroundColor White
    Write-Host "1. Install SQLite CLI and run this script again" -ForegroundColor White
    Write-Host "2. Use a SQLite GUI tool to run the SQL script" -ForegroundColor White
    Write-Host "3. Run the EF migrations once database conflicts are resolved" -ForegroundColor White
}

# Alternative: Try using .NET to execute the SQL
try {
    Write-Host "Attempting to apply using .NET..." -ForegroundColor Yellow
    
    # Use dotnet ef to generate the specific SQL for this migration only
    $migrationSql = dotnet ef migrations script 20250809174619_AddIsExpenseField 20250811121700_AddProjectsTable --no-build
    
    Write-Host "Generated migration SQL for Projects table." -ForegroundColor Green
    Write-Host "You can apply this SQL manually to your database." -ForegroundColor Yellow
    
} catch {
    Write-Host "Could not generate migration SQL automatically." -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Manual Steps ===" -ForegroundColor Cyan
Write-Host "1. Open your SQLite database tool (DB Browser for SQLite, etc.)" -ForegroundColor White
Write-Host "2. Run the SQL from Database\CreateProjectsTable.sql" -ForegroundColor White
Write-Host "3. Verify the Projects table exists" -ForegroundColor White
Write-Host "4. Test creating a project in your application" -ForegroundColor White