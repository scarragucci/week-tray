# WeekTray

Windows tray icon that shows the current ISO 8601 week number.

- No network, no admin rights, no installer.
- Needs only the .NET Framework 4 that ships with Windows.
- Right-click the icon for the menu; "Start with Windows" toggles an opt-in `HKCU\...\Run` entry.

## Build

Uses the in-box C# compiler (`csc.exe`, C# 5), so there is nothing to install:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

This produces `WeekTray.exe` and exports `icons\week-01..53.ico` plus `icons\app.ico`.
Exit a running WeekTray first, or the exe stays locked.

## Usage

```
WeekTray.exe                      run in the tray
WeekTray.exe --export-icons DIR   write week-01..week-53.ico + app.ico, then exit
```
