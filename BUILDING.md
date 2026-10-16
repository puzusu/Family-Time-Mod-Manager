# Building from source

## Tooling

- Windows x64
- .NET 8 SDK
- PowerShell
- Internet access during the first publish, so the .NET runtime packs can be restored from NuGet

## Required release resources

The source tree intentionally does not include game-derived artwork or the compatibility archive. Place the following files in `Assets/` before publishing:

| File | How to obtain it | SHA-256 |
| --- | --- | --- |
| `frame.png` | From a copy of Family Time you are authorized to use, or replace with original artwork | — |
| `control-frame.png` | From a copy of Family Time you are authorized to use, or replace with original artwork | — |
| `BepInEx_win_x64_5.4.23.5.zip` | Official [BepInEx 5.4.23.5 release](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) | `82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4` |
| `FamilyTime-b108-unstripped-compat.zip` | Compatibility archive from the b108 development setup; not included because it contains game/runtime assemblies | `1A3F4F705DA33606FF98527905225DDC95DF7602FC045142D68524403C98A41B` |

Verify each archive’s hash before using it. Do not commit game-derived artwork or compatibility assemblies to a public repository unless you have redistribution rights.

## Publish

From the repository root, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\BuildSingleFile.ps1
```

The script restores the project for `win-x64`, publishes a self-contained single-file application, and writes the executable to `share/FamilyModManager-0.8.2-b108.exe`.

The exact release can only be reproduced with the same source revision and matching embedded resources. Replacing the game-derived artwork will produce a visually different build.
