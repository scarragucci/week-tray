# Builds WeekTray.exe with the C# compiler that ships with Windows, then exports the icons.
# Usage:  powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

function Compile([string[]]$extra) {
    & $csc /nologo /target:winexe /optimize+ /out:WeekTray.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll @extra WeekTray.cs
    if ($LASTEXITCODE) { throw "csc failed ($LASTEXITCODE)" }
}

Compile @()                                   # pass 1: no exe icon yet
Start-Process .\WeekTray.exe -ArgumentList '--export-icons', 'icons' -Wait
Compile @('/win32icon:icons\app.ico')         # pass 2: embed app icon
Write-Host "Built WeekTray.exe + $((Get-ChildItem icons -Filter week-*.ico).Count) week icons"
