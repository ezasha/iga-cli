# igArchiveExtractor CLI (Interactive CMD + Command Line)

A lightweight .NET 6 Windows CLI tool to inspect and extract Skylanders-style archives and IGZ textures.

This repository is now trimmed to keep only files required to build and run the current CLI version.

## Features

- Interactive CMD menu (`L / E / S / G / H / Q`)
- Windows file/folder dialogs in interactive mode
- Archive operations:
  - List files (`index`, `size`, `path`)
  - Extract all files
  - Extract a single file by index (with pre-listing)
- IGZ texture operations:
  - List image entries (`index`, `size`, `dimensions`, `name`)
  - Extract images to PNG
  - Uses real IGZ names when available, normalized for Windows filenames
- Colored console output for status/success/errors

## Supported Inputs

- Archives: `.arc`, `.bld`, `.pak`, `.iga`
- IGZ textures: `.igz` and IGZ-based `level.bld`

## Game Mapping Status

- `Skylanders Spyro's Adventure (3DS/Wii)`
- `Skylanders Spyro's Adventure (Wii U)`
- `Skylanders Giants (3DS)`
- `Skylanders Giants (Home Console)`
- `Skylanders Giants (Home Console Alpha)`
- `Skylanders Swap Force (3DS)`
- `Skylanders Swap Force (Home Console)`
- `Skylanders Swap Force (Home Console Alpha)`
- `Skylanders Trap Team (3DS)`
- `Skylanders Trap Team (Home Console)`
- `Skylanders SuperChargers`
- `Skylanders Imaginators (PS3/X360/Wii U)`
- `Skylanders Imaginators (PS4)`

## Requirements

- Windows 10/11 (or compatible Windows console host)
- .NET 6 Runtime installed on target machine
- SDK only needed for development/build

## Build (Development)

From repository root:

```powershell
dotnet restore .\CLI.csproj
dotnet build .\CLI.csproj -c Release
```

## Publish (Recommended lightweight package)

```powershell
dotnet publish .\CLI.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\publish-lite
```

Output files (expected):
- `publish-lite/iga-cli.exe`
- `publish-lite/iga-cli.dll.config`
- optional debug symbol if enabled

## Run

### Interactive mode (recommended)

```powershell
.\publish-lite\iga-cli.exe
```

### Command mode

```powershell
# List archive files
.\publish-lite\iga-cli.exe list "game.bld" --game SkylandersTrapTeam

# Extract all files
.\publish-lite\iga-cli.exe extract "game.bld" "out" --game SkylandersTrapTeam

# Extract one file by index
.\publish-lite\iga-cli.exe extract-single "game.bld" 12 "out" --game SkylandersTrapTeam

# List IGZ images
.\publish-lite\iga-cli.exe igz-list-images "level.bld"

# Extract IGZ images to PNG
.\publish-lite\iga-cli.exe igz-extract-images "level.bld" "out_images"

# Show game mappings
.\publish-lite\iga-cli.exe list-games
```

## Interactive Menu

When started without arguments:

- `L` List archive contents
- `E` Extract all files (or IGZ images)
- `S` Extract single archive file
- `G` Show game mapping list
- `H` Help page (CLI + Python examples)
- `Q` Quit

## Python Usage (example via subprocess)

```python
import subprocess

exe = r"publish-lite\iga-cli.exe"
subprocess.run([exe, "list", "game.bld", "--game", "SkylandersTrapTeam"], check=True)
subprocess.run([exe, "igz-extract-images", "level.bld", "out_images"], check=True)
```

## Repository Layout (kept intentionally minimal)

- `CLI.csproj` - active project
- `Program_CLI.cs` - entrypoint + interactive/CLI logic
- `StreamHelper.cs`
- `IGA/`, `IGZ/`, `Types/`, `Utils/`, `GX2/` - parsing and texture logic
- `igae.ico` - executable icon
- `build.ps1` - helper build/publish script
- `igArchiveExtractor-open-source.sln` - solution pointing to CLI project
- `.gitignore`, `.gitattributes`, `LICENSE`

## Notes

- `bin/` and `obj/` are generated automatically and ignored by Git.
- If publish fails with file lock (`iga-cli.exe in use`), close running instances and republish.
- Console scrollback clearing behavior can depend on the host terminal.
