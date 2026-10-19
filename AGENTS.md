# AGENTS.md

Guidance for AI coding agents working in the Hst Imager repository.

## Project overview

Hst Imager is a cross-platform (Windows, macOS, Linux) imaging tool for reading and writing disk images to and from physical drives (USB disks, CF/SD cards). It has a strong Amiga focus: Rigid Disk Block (RDB) partition tables, Amiga file systems (PFS3, FFS/DOS7, ...), ADF/HDF images, PiStorm RDB, UAE metadata. It also handles MBR, GPT, FAT32, exFAT, ext, ISO9660, VHD and compressed images (.zip, .gz, .xz, .rar, .lha, .lzx).

> Raw disk access is destructive. Never run commands against real physical drives while testing. Tests and manual experiments must use image files only.

## Repository layout

All .NET code lives under `src/` and is built from `src/Hst.Imager.sln`.

| Project | Purpose |
| --- | --- |
| `Hst.Imager.Core` | All imaging logic: commands (`Commands/`), physical drive access per OS (`PhysicalDrives/`), partition tables, file systems, compression, sparse files, UAE metadata. UI-agnostic. |
| `Hst.Imager.ConsoleApp` | CLI built on `System.CommandLine`. `*CommandFactory.cs` define commands/options, `CommandHandler.cs` maps them to Core commands, `Presenters/` format output. Usage documented in `usage.md`. |
| `Hst.Imager.AvaloniaApp` | New GUI (Avalonia 12 + ReactiveUI, MVVM): `Views/` (`.axaml`), `ViewModels/`, `Services/` (`ImagingService` wraps Core commands), `Controls/`. Packaged with Velopack. This is the GUI that is released. |
| `Hst.Imager.GuiApp` | Legacy GUI (ASP.NET Core + SignalR + Electron.NET, React/MUI client in `ClientApp/`). Being replaced by the Avalonia app. |
| `Hst.Imager.Core.Tests` | xUnit tests for Core. |
| `Hst.Imager.ConsoleApp.Tests` | xUnit tests for CLI parsing. |

Other notable files:
- `src/Directory.Build.props` - target framework (`net10.0`) and the single source of truth for `<Version>`.
- `src/Directory.Build.targets` - replaces the last version part with `git rev-list --count HEAD` (needs full git history).
- `src/.config/dotnet-tools.json` - local tools (`paket`, `vpk`).
- `.github/workflows/` - `build-and-test.yml` (every push, Windows/macOS/Linux) and `build-and-release.yml`.
- `release-notes.md`, `todo.md`, `file-formats.md` at the repo root.

## Build and test

Run from `src/`:

```
dotnet tool restore
dotnet restore
dotnet build
dotnet test --no-build -m:1
```

- Tests must run with `-m:1` (not in parallel across projects); many tests create image files in the working directory.
- Run a single test class with `dotnet test Hst.Imager.Core.Tests --filter "FullyQualifiedName~GivenReadCommandWithRdb"`.
- Run the Avalonia app with `dotnet run --project Hst.Imager.AvaloniaApp`. Physical drive access requires administrator/sudo; image files work without it.
- Run the console app with `dotnet run --project Hst.Imager.ConsoleApp -- <command> ...`.
- Build must stay warning-free in the Avalonia app.

## Architecture notes

- Every operation is a Core command deriving from `CommandBase` (`Execute(CancellationToken)` returning `Hst.Core.Result`), reporting progress via `DataProcessed` and messages via `DebugMessage`/`WarningMessage`/`InformationMessage` events.
- Media is resolved through `ICommandHelper` (`CommandHelper`), which handles physical drives, image files, compressed files, VHD and path modifiers (e.g. `+bs:` for byte swap) and partition paths like `disk.img\mbr\2` or `disk.img\rdb\dh0`.
- Both the console app and the Avalonia app are thin front-ends over Core commands. Put behavior in Core so both front-ends share it; add a CLI option when a new capability is added to a command.
- Amiga-specific functionality largely comes from the `Hst.Amiga`, `Hst.Core` and `hst.compression` NuGet packages (maintained by the same author). Disk/container support uses `LTRData.DiscUtils.*`.
- Avalonia app: views bind with compiled bindings (`AvaloniaUseCompiledBindingsByDefault`), view models derive from `ViewModelBase` (`ReactiveObject`), services are registered via `Microsoft.Extensions.DependencyInjection`.
- The Avalonia app must provide the same tasks and behavior as the legacy GuiApp (functional parity), but does not need to replicate its UI exactly.

## Code conventions

- C#, 4-space indentation, CRLF line endings. Match the style of the file being edited: older Core/ConsoleApp code uses block namespaces with `using` directives inside the namespace; newer code (Avalonia app) uses file-scoped namespaces with usings at the top.
- Private fields are `_camelCase`; async methods in services use the `Async` suffix.
- Prefer returning `Result`/`Result<T>` with typed errors (e.g. `PathNotFoundError`) from Core commands over throwing.
- Tests are BDD style: classes named `Given...`, methods named `When...Then...`, bodies with `// arrange`, `// act`, `// assert` comments. Use `TestCommandHelper` and in-memory/test media, `NullLogger<T>` for loggers, and unique (`Guid.NewGuid()`) file names for any files written to disk.
- Add a test in `Hst.Imager.Core.Tests` when fixing a bug or adding behavior in Core.

## Git

- Main branch is `main`. Commit messages are a single plain imperative sentence describing the change (e.g. "Fix FastFileSystem DOS7 support check in format command"), no conventional-commit prefixes.
