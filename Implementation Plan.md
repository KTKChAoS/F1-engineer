# PitWall F1 25 — Implementation Plan

## Current Status: 🚧 M2 LAN Acceptance Pending; M3 Widgets — In Progress

Progress was reconciled against the repository on 30 October 2026. A checked item means there is a concrete implementation or artifact in the repo; acceptance criteria and tests remain unchecked until they are present and verified.

---

## M0 — Skeleton (Complete)
**Goal:** Solution builds, CI runs build + tests, Contracts defined.

### Steps:
1. [x] Read spec & align project targets with .NET 10 LTS; verify F1Game.UDP 25.1.1 decision
2. [x] Create solution & project structure per spec §5
3. [x] Add NuGet packages (F1Game.UDP, Serilog, xUnit; System.Text.Json is part of .NET)
4. [x] Define PitWall.Contracts: DTOs, message types, constants
5. [x] Create docs/ scaffolding (setup.md, protocol.md, decisions.md, testing.md)
6. [x] Verify solution builds with `dotnet build` (Release, .NET 10)
7. [x] Add GitHub Actions CI workflow (build, xUnit tests, web production build)
8. [x] Commit M0 (see repository history)

### Key Decisions:
- .NET 10 LTS — record in decisions.md
- F1Game.UDP 25.x via NuGet for packet decoding

---

## M1 — POC Listener (Complete)
**Goal:** Host shows PPS + decoded player telemetry from PS5 or Sim; recorder/replayer work.

### Steps:
1. [x] Implement `UdpReceiver` with bounded Channel (§8.1)
2. [x] Implement `PacketDecoder` wrapping F1Game.UDP (§8.2) — validates/decode packets, tracks sessions and stale frames; round-trip coverage added
3. [x] Basic `StateStore` for player telemetry (§8.4) — player telemetry and some session/event data exist; full field state and lap history remain for M2
4. [x] `Recorder` writing .pwrec files (§8.6)
5. [x] `Replayer` feeding recordings at 1x/2x/4x (§8.6) — reusable API supports speed and looping; Host UI currently exposes 1x replay only
6. [x] `PitWall.Sim` tool: synthetic 22-car, 5-lap race generator (§8.6), including Session, Lap Data, Car Telemetry, Car Status, Car Damage, Participants, Tyre Sets, Session History, Event and Motion packets
7. [x] Host WPF showing connection state, PPS, and player data
8. [x] Unit tests: decoder packet round-trips, malformed/stale handling, and recorder round-trip
9. [x] Accept: replay a recording → same values — replayed a 14.8 s live capture through `Replayer` to an isolated UDP listener: all 5,359 datagrams matched byte-for-byte and decoded; 0 malformed or stale packets

---

## M2 — State & Local Dashboard
**Goal:** Full state store, snapshot builder, Kestrel + WebSocket, React MVP widgets 1-4.

### Steps:
1. [x] Complete `StateStore` with all 22 car slots, event history, and completed player lap log (§8.4)
2. [x] `SnapshotBuilder` emits `live`/`field`/`event` payloads (§8.5)
3. [x] Kestrel server with WebSocket endpoint (§10)
4. [x] Static file serving for web client
5. [x] React/Vite/TypeScript/Zustand web client scaffold (§4)
6. [x] TypeScript types generated from Contracts by `tools/PitWall.TypeGen`; web production builds regenerate the types
7. [x] Widget 1: Timing Tower
8. [x] Widget 2: Driver Card (speed, gear, RPM, throttle, brake, DRS)
9. [x] Widget 3: Tyre Diagram (wear, temps, pressure, compound)
10. [x] Widget 4: Fuel & ERS
11. [x] Dashboard header (session, track, lap, weather, connection)
12. [ ] Accept: phone on LAN sees live data

---

## M3 — Full MVP & Calls
**Goal:** Widgets 5-10, driver view, call system, audio, alerts.

### Steps:
1. [x] Widget 5: Gap Ahead/Behind with per-lap trend arrows
2. [x] Widget 6: Lap Table (last 10 laps, sectors, validity, compound)
3. [ ] Widget 7: Damage Panel
4. [ ] Widget 8: Weather Panel + forecast
5. [ ] Widget 9: Events Feed
6. [ ] Widget 10: Call Panel (presets, text, status)
7. [ ] Driver view (/driver): dark, high-contrast, minimal
8. [ ] Call routing: engineer → host → driver views
9. [ ] Audio: NAudio + Windows speech synthesis on host
10. [ ] Driver view: alert tone, vibration, Wake Lock, speech
11. [ ] Alert rules (tyre wear, fuel, damage, penalties, rain)
12. [ ] Derived metrics (§14)

---

## M4 — Relay & Room Codes
**Goal:** Remote engineer joins via room code, no port forwarding.

### Steps:
1. [ ] `PitWall.Relay` ASP.NET Core minimal API
2. [ ] Room code generation (30-symbol alphabet, §11.2)
3. [ ] Host WebSocket (createRoom, hostSecret, resume)
4. [ ] Viewer WebSocket (hello, approval, viewerToken)
5. [ ] Message forwarding (live/field/event → viewers, calls → host)
6. [ ] Join page at /join with code input
7. [ ] Host Share panel (code, QR, link, viewer list, kick)
8. [ ] Rate limiting (§11.4)
9. [ ] Reconnect with exponential backoff (§11.5)
10. [ ] Dockerfile + fly.toml (§15)
11. [ ] Integration tests: fake host + viewers

---

## M5 — Phase 2 & Polish
**Goal:** Track map, strategy planner, charts, installer.

- Track map (Canvas + motion data)
- Strategy planner (pit window, stints, tyre inventory)
- Lap time chart, tyre wear chart, position chart (uPlot)
- Setup viewer, session summary, CSV export
- Settings (units, theme, saved layouts, colour-blind palette)
- Self-contained win-x64 installer
