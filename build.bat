@echo off
REM 1) sumatra\SumatraPDF.exe is folder me rakh do (portable 64-bit version)
REM 2) Ye file chalao
if not exist sumatra\SumatraPDF.exe (
  echo sumatra\SumatraPDF.exe missing - pehle download karke rakho
  exit /b 1
)
dotnet publish AutoPrintAgent.csproj -c Release -o publish || exit /b 1
where iscc >nul 2>nul && iscc installer.iss || echo Inno Setup nahi mila - installer.iss ko manually compile karo
echo Done. dist\AutoPrintAgent-Setup.exe dekho
