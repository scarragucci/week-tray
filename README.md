# WeekTray

Windows tray icon that shows the current ISO 8601 week number.

- No network, no admin rights, no installer.
- Needs only the .NET Framework 4 that ships with Windows.
- Right-click the icon for the menu; "Start with Windows" toggles an opt-in `HKCU\...\Run` entry.

## Download and run

1. Download **[WeekTray.exe](https://github.com/scarragucci/week-tray/releases/latest/download/WeekTray.exe)** (or pick a version on the [Releases](https://github.com/scarragucci/week-tray/releases) page).
2. Double-click it. The week number appears in the tray, bottom-right. If you don't see it, click the `^` arrow for hidden icons.
3. Optional: right-click the icon and choose **Start with Windows**.

The exe is not code-signed, so the first time Windows SmartScreen may say "Windows protected your PC".
Click **More info**, then **Run anyway**.

## Build it yourself

Uses the in-box C# compiler (`csc.exe`, C# 5), so there is nothing to install.

1. Click **Code > Download ZIP** and extract the whole ZIP. Don't run the build from inside the ZIP.
2. Double-click `build.cmd`.

Or from a terminal:

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
