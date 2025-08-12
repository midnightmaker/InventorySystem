@echo off
echo === Complete Database Reset ===
echo.

echo Deleting existing database...
if exist inventory.db (
    echo Creating backup...
    copy inventory.db inventory_backup_%date:~-4,4%%date:~-10,2%%date:~-7,2%_%time:~0,2%%time:~3,2%%time:~6,2%.db
    del inventory.db
    echo Database deleted!
) else (
    echo No existing database found.
)

echo.
echo Deleting migration files...
if exist Migrations (
    rmdir /s /q Migrations
    echo Migration files deleted!
) else (
    echo No migrations folder found.
)

echo.
echo === Reset Complete ===
echo.
echo Next steps:
echo 1. Run your application
echo 2. Database will be created automatically from your models
echo 3. Projects table will be included
echo 4. Sample data will be seeded
echo.
echo Press any key to continue...
pause > nul