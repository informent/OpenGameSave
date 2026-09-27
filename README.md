# OpenGameSave

OpenGameSave is a local-first Windows utility for protecting game saves with clear, inspectable backups.

## What it does

- Detects installed Steam games from Steam's library manifests.
- Shows selected game artwork when available.
- Finds common save locations, including Steam userdata and standard Windows folders.
- Creates timestamped snapshots with SHA-256 hashes and a JSON manifest.
- Verifies snapshots before restoring and refuses to overwrite files.
- Keeps data on your computer. No account, telemetry, or cloud service is required.

## Use

1. Start OpenGameSave.
2. Select **Detect Steam games** and choose a game.
3. Select **Find save folders**. If no single safe candidate is found, choose the folder manually.
4. Choose a backup library on a different drive when possible.
5. Create snapshots and restore them from the history panel.

OpenGameSave never silently overwrites files.

## Build and test

```powershell
dotnet run --project tests/SaveEngineTests.csproj
dotnet run --project tests/SteamDiscoveryTests.csproj
dotnet run --project tests/SaveLocationTests.csproj
dotnet build OpenGameSave.csproj -c Release
```

GitHub Actions runs these checks on Windows. Release packages are published in GitHub Releases.

## License

OpenGameSave is released under the MIT License. See [LICENSE](LICENSE).
