# Privacy Filter

A modern Windows application that provides virtual privacy filtering for selected applications through customizable window blur effects.

## Features

- **Process Management**: View all running processes with visible windows
- **Privacy Control**: Select applications to apply privacy blur effects
- **Customizable Blur Levels**: Adjust blur intensity from 0-100% (default 95%)
- **Smart Window Monitoring**: Optionally removes blur when window becomes active (configurable per process)
- **Modern UI**: Clean, dark-themed interface built with WPF

## Technology Stack

- **.NET 10.0** - Latest Long Term Support (LTS) .NET framework
- **WPF** - Windows Presentation Foundation for UI
- **Windows APIs** - Direct integration with Windows window management
- **MVVM Pattern** - Clean architecture with ViewModels

## Requirements

- Windows 10/11
- .NET 10.0 Runtime

### Administrator privileges

Administrator privileges are **optional**.

- Running without admin works for most normal applications.
- Elevation is only needed if you want to blur/unblur windows of applications running **as Administrator** (higher integrity level).

## Installation

### Option 1: Using the Installer (Recommended)

1. Download or build the MSI installer
2. Double-click `PrivacyFilterInstaller.msi` to install
3. Launch from Start Menu or Desktop shortcut

### Option 2: Build from Source

1. Clone this repository
2. Build the solution using Visual Studio 2022 (or later) or `dotnet build`
3. Run the application (optionally as Administrator)

### Building the Installer

To create the MSI installer:

1. Install WiX Toolset (the build script will do this automatically):
   ```powershell
   dotnet tool install --global wix
   ```

2. Run the build script:
   ```powershell
   .\temp\build-installer.ps1
   ```

3. The installer will be created at: `Installer\bin\Release\PrivacyFilterInstaller.msi`

The installer includes:
- Main application executable and dependencies
- Start Menu shortcut
- Desktop shortcut
- Automatic updates support (same installer can update existing installations)
- Clean uninstallation through Windows Settings

## Usage

1. **Launch the Application**: Run PrivacyFilter.exe
2. **View Running Processes**: The left panel shows all processes with visible windows
3. **Add to Private List**: Select a process and click "Add to Private" (default blur: 95%)
4. **Adjust Blur Level**: Use the slider to set blur intensity (0-100%)
5. **Configure Auto-Unblur**: Toggle the "Auto-Unblur" checkbox per process to control whether blur is removed when the window gains focus
6. **Automatic Privacy**: Windows will blur when not active, and optionally clear when focused (based on Auto-Unblur setting)

### Tray

- The app can **minimize to the system tray on startup** (default enabled). You can toggle this via the in-app checkbox or the tray context menu.
- Use the tray icon context menu to open the window or exit.

### Restart as Administrator

If you need elevation to control elevated windows, use the **Restart as Administrator** button (or the tray menu item).

## How It Works

The application uses Windows APIs to:
- Enumerate running processes with visible windows
- Apply layered window attributes for transparency effects
- Monitor window focus changes via Windows event hooks
- Dynamically adjust blur based on window state
- Separate browser processes (Chrome, Edge, Firefox, etc.) by window/tab for individual control
- Persist settings to `%AppData%\PrivacyFilter\settings.json` for automatic restoration on restart

## Security

- Administrator privileges are optional; required only to manipulate windows of elevated processes
- Only affects windows of explicitly selected processes
- No data collection or network communication
- All processing happens locally

## Development

Built with modern C# and .NET 10.0 features:
- Nullable reference types
- Async/await patterns
- Dependency injection ready
- Clean MVVM architecture
- Per-process blur configuration

## License

This project is provided as-is for educational and personal use.
