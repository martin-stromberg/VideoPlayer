@echo off
setlocal enabledelayedexpansion

dotnet build "c:\Users\Martin\Documents\Repositories\VideoPlayer\VideoWebPlayer.Maui\VideoWebPlayer.Maui.csproj" -f net9.0-android --no-restore

exit /b %ERRORLEVEL%
