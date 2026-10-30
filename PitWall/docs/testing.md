# PitWall — Testing Guide

## Automated checks

From the repository root:

```powershell
dotnet build PitWall/PitWall.sln
dotnet test PitWall/PitWall.sln
```

The current xUnit tests cover packet encoder/decoder round trips, malformed and stale packets, recorder round trips, and relay message contracts. Live PS5 acceptance remains manual.

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

To replay a saved recording into a running local Host from the repository root:

```powershell
dotnet run --project PitWall/tools/PitWall.Sim -- --replay "recordings/Melbourne_Race_20261030_215328.pwrec" --speed 4
```

Replay speed accepts `1`, `2`, or `4`; add `--loop` to repeat until Ctrl+C. Use `--address` and `--port` to target another UDP listener.

For the M4 data path, the field snapshot should contain `trackPath`, `positionHistory`, `setup`, `laps`, and `strategy` data when the recording includes those packet types. The track map renders on Canvas and the three analysis charts use uPlot, as required by the spec. Screenshot-based acceptance on 30 October 2026 used the user's live Melbourne dashboard captures to review telemetry, the map, charts, session summary, and CSV control; the user had previously confirmed the dashboard on a phone. That review found duplicate lap-axis labels and reversed trend direction, which were corrected in the current build.
