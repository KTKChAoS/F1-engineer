# PitWall — Testing Guide

## Automated checks

From the repository root:

```powershell
dotnet build PitWall/PitWall.sln
dotnet test PitWall/PitWall.sln
```

The current test projects are xUnit scaffolds. Decoder fixtures, recorder round-trip coverage, relay integration tests, and web tests are added in their respective milestones.

For the web client, run from `PitWall/src/PitWall.Web`:

```powershell
npm install
npm run build
```

## Manual telemetry smoke check

1. Start `PitWall/src/PitWall.Host` with `dotnet run` on Windows.
2. Configure F1 25 to send UDP Format 2025 to the PC on port 20777, or use a compatible replay recording.
3. Confirm the Host reports Receiving, a nonzero packet rate, packet format 2025, and changing player telemetry.
4. Start and stop a recording, replay it, and confirm the displayed values update.

The real-PS5 acceptance checklist and captured decoder fixtures remain future work.
