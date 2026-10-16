# Family Time Mod Manager

A standalone Windows app for managing BepInEx plugins for **Family Time b108**. It provides a game-styled interface for installing BepInEx, adding plugins, and enabling or disabling them between game sessions.

![Family Mod Manager interface](docs/manager-ui.png)

## Download

Download the latest [FamilyModManager-*-b108.exe](https://github.com/puzusu/Family-Time-Mod-Manager/releases/download/mod/FamilyModManager-0.8.2-b108.exe) from the **Releases** section. The application is self-contained; users do not need to install .NET separately.

The executable is distributed as a GitHub Release asset rather than committed to the source repository. This keeps the repository focused on reviewable source and avoids storing large binaries in Git history.

## Features

- Finds BepInEx plugins and displays their names, versions, IDs, file locations, and enabled state.
- Inspects plugin metadata without loading or executing plugin DLLs.
- Adds plugin DLLs and plugin-only ZIP packages, with checks for duplicate IDs, unsafe archive paths, and loader packs.
- Enables and disables plugins by moving their DLLs between the active and disabled plugin folders. Changes apply on the next game launch.
- Installs the BepInEx 5.4.23.5 Windows x64 loader and the compatibility files needed by Family Time b108.
- Opens the game’s plugin, configuration, and log folders, and can launch the selected game.
- Refuses plugin or loader changes while Family Time is running. It does not modify save files.

## Requirements

- Windows x64
- Family Time b108
- BepInEx 5.4.23.5 for plugin support

## Build from source

See [Building from source](BUILDING.md) for the SDK requirements, required assets, integrity checks, and build command.

The public source repository omits game-derived UI artwork and the compatibility archive. These resources are not redistributed here. As a result, a source build requires the matching resources before it can reproduce the complete release.

## Safety notes

The manager reads plugin assembly metadata for display. It does not load plugin code into itself, and it does not run imported plugins. Plugins are loaded by BepInEx when the game starts. Only install plugins you trust.

## Project files

| Path | Purpose |
| --- | --- |
| `Program.cs`, `MainForm.cs` | Application startup and main window |
| `PluginCatalog.cs` | Plugin metadata inspection and safe ZIP import |
| `BepInExInstaller.cs` | Loader installation and resource checks |
| `GameSkin.cs` | Game-inspired window and controls |
| `BuildSingleFile.ps1` | Self-contained Windows release build |
| `docs/` | Release and source review notes |

## License

The project does not currently include a license for reuse of its application code. The Neuton Cursive font is distributed under the SIL Open Font License; see [`Assets/OFL.txt`](Assets/OFL.txt).
