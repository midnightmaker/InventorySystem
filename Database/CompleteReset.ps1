# Complete Database Reset Script
# This will delete everything and create a fresh database from your models

Write-Host "=== Complete Database Reset (No Migrations) ===" -ForegroundColor Green
Write-Host ""

# Step 1: Delete the database file
$dbFile = "inventory.db"
if (Test-Path $dbFile) {
    Write-Host "Creating backup of existing database..." -ForegroundColor Yellow
    $backupFile = "inventory_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').db"
    Copy-Item $dbFile $backupFile -ErrorAction SilentlyContinue
    Write-Host "Backup created: $backupFile" -ForegroundColor Cyan
    
    Write-Host "Deleting existing database..." -ForegroundColor Yellow
    Remove-Item $dbFile -Force
    Write-Host "Database deleted!" -ForegroundColor Green
} else {
    Write-Host "No existing database found." -ForegroundColor Cyan
}

# Step 2: Delete all migration files
$migrationsFolder = "Migrations"
if (Test-Path $migrationsFolder) {
    Write-Host "Deleting all migration files..." -ForegroundColor Yellow
    Remove-Item "$migrationsFolder\*" -Recurse -Force
    Write-Host "Migration files deleted!" -ForegroundColor Green
} else {
    Write-Host "No migrations folder found." -ForegroundColor Cyan
}

Write-Host ""
Write-Host "=== Next Steps ===" -ForegroundColor Cyan
Write-Host "1. Modify program.cs to use EnsureCreated instead of migrations" -ForegroundColor White
Write-Host "2. Run your application" -ForegroundColor White
Write-Host "3. Database will be created automatically with all your models" -ForegroundColor White
Write-Host "4. Projects table will be included automatically" -ForegroundColor White

Write-Host ""
Write-Host "? Reset complete! Your app will create a fresh database on next startup." -ForegroundColor Green