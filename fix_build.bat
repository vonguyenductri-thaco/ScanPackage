@echo off
echo ============================================
echo   QUICK FIX - File Locking Build Error
echo ============================================
echo.

echo [14] Stopping Android processes...
taskkill F IM qemu-system-.exe 2nul
taskkill F IM adb.exe 2nul
taskkill F IM emulator.exe 2nul
echo      Done!

echo.
echo [24] Cleaning obj directory...
cd CUsersLENOVOsourcereposScanPackage
if exist obj (
    rmdir s q obj
    echo      obj deleted
) else (
    echo      obj not found
)

echo.
echo [34] Cleaning bin directory...
if exist bin (
    rmdir s q bin
    echo      bin deleted
) else (
    echo      bin not found
)

echo.
echo [44] Done!
echo.
echo ============================================
echo   Now open Visual Studio and Rebuild
echo ============================================
echo.
pause