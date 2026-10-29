# PitWall: F1 25 Race Engineer Dashboard - Project Specification

*Version 1.0 | 29 October 2026 | Owner: Kartik | Audience: an AI coding agent that will build the complete system from this document*

## 1. Purpose and scope

PitWall is a free, open-source Windows application that receives the UDP telemetry broadcast by EA SPORTS F1 25 (played on a PS5), turns it into a live race-engineer dashboard, and lets a second person (the engineer) watch it and send short radio-style calls to the driver. The engineer can be on the same home network or anywhere on the internet, joining remotely with a room code.

In scope: UDP ingestion and decoding, live race state, local and remote dashboards, engineer-to-driver calls, session recording and replay, and a relay service for remote viewers.

Out of scope: an AI race engineer or voice assistant; controlling the game (the game's UDP telemetry is output-only, so nothing can be sent into it and the app cannot trigger an in-game pit request); special handling of online-lobby telemetry privacy (the owner mainly plays offline career mode, so the app only has to tolerate missing data without crashing); accounts, payments, or any paywalled feature.

## 2. Roles

- Host: runs the Windows app next to the PS5. Only the host can create or close a room, approve or kick viewers, and start recordings.
- Driver view: a phone or tablet browser page near the player (LAN). Minimal, shows engineer calls and key stats.
- Engineer: a viewer with the full dashboard and call buttons, either on the LAN or remote via room code.

## 3. Success criteria

- POC: with the PS5 sending to the PC, live speed, gear, lap, tyre and fuel values appear in the host UI within 200 ms of the packet, at send rates from 20 to 60 Hz, with no crash on truncated, malformed, or unknown packets.
- Full: a remote engineer joins using only a room code in under 30 seconds, with no port forwarding and no install; added latency under 500 ms on normal broadband; a BOX call plays an alert on the driver view within 1 second.

## 4. Fixed technology decisions

Do not substitute without a recorded reason in docs/decisions.md.

- Runtime: C# on .NET 10 (LTS); .NET 8 only if 10 is unavailable.
- Decoding: NuGet package F1Game.UDP 25.x (targets F1 25). Do not hand-write byte offsets or enum values; use the library or the official EA spec. If a needed packet is missing from the library, implement it from the spec and add a unit test with a captured packet.
- Host: WPF control-panel window plus an embedded ASP.NET Core (Kestrel) server in the same process.
- Wire protocol: JSON text frames over WebSocket, System.Text.Json, camelCase, UTF-8.
- Relay: ASP.NET Core minimal app, in-memory state, single instance, deployable by Docker (Fly.io, Railway, or any VPS).
- Web client: TypeScript, React, Vite, Zustand, Tailwind CSS, uPlot for charts, Canvas for the track map. Built to static files embedded in the Host and served by the Relay.
- Tests: xUnit, Vitest, optional Playwright. Logging: Serilog. Config: appsettings.json plus %APPDATA%/PitWall/settings.json.

## 5. Repository layout

```
PitWall/
  PitWall.sln
  src/PitWall.Contracts/  shared DTOs, message types, constants
  src/PitWall.Core/       receiver, decoder adapter, state store, derived metrics, snapshot builder, recorder, replayer
  src/PitWall.Host/       WPF app, Kestrel server, relay client, audio
  src/PitWall.Relay/      relay service + Dockerfile
  src/PitWall.Web/        React client: engineer, driver, join pages
  tools/PitWall.Sim/      console tool: replay recordings or generate synthetic telemetry
  tests/                  PitWall.Core.Tests, PitWall.Relay.Tests
  docs/                   setup.md, protocol.md, decisions.md, testing.md
```

## 6. Architecture and data flow

```
PS5 --UDP--> UdpReceiver -> PacketDecoder -> StateStore -> SnapshotBuilder
   -> LocalHub (LAN WebSocket) -> browsers on LAN
   -> RelayClient (outbound WebSocket) -> Relay -> remote browsers
Recorder taps raw datagrams. Calls flow back: browser -> Hub or Relay -> Host -> driver views.
```

Principles:

- The Host is the single source of truth. Browsers and the Relay never decode UDP.
- Network I/O must never block the UDP loop: use a bounded Channel of byte arrays, drop-oldest, capacity 1024.
- Units in messages: milliseconds, metres, degrees Celsius, kilograms, km/h, PSI as sent by the game. Convert (for example to mph) only in the web client.
- Car index (0-21) is the key for all per-car data. Wheel arrays are ordered RL, RR, FL, FR, as in the game spec.

## 7. Game and network setup

Document in docs/setup.md and show on the app's first-run screen.

- PS5, F1 25, Settings > Telemetry Settings: UDP Telemetry On; UDP Broadcast Mode Off; UDP IP Address = the PC's IPv4 address; UDP Port 20777; UDP Send Rate 20 Hz (up to 60 Hz supported); UDP Format 2025. If the packet header reports a different format, show a clear warning to set UDP Format to 2025.
- Reserve a fixed IP for the PC in the router.
- Windows Firewall: inbound UDP on the telemetry port and inbound TCP on the local web port (default 5080). Offer a button that adds both rules through netsh (needs elevation) plus copy-paste commands as a fallback.
- First-run screen: detected private IPv4 addresses (192.168.x.x, 10.x.x.x, 172.16-31.x.x), a waiting indicator that turns green on the first valid packet, packets per second, last-packet age, and format version.

## 8. PitWall.Core

### 8.1 UdpReceiver

Bind UdpClient to 0.0.0.0 on the configured port (default 20777), show a friendly error if the port is taken, loop on ReceiveAsync, write datagrams into the channel, and count received, dropped, and malformed. Connection state: Waiting, Receiving (packet in the last 3 s), Stale.

### 8.2 PacketDecoder

- Wrap F1Game.UDP: bytes to typed packets. Validate the 29-byte header (packet format, game year, version bytes, packet id, session UID, session time, frame identifier, overall frame identifier, player car index, secondary player car index).
- When a new session UID appears, reset race state. Drop packets older than the last applied frame for the same packet id.
- Never throw out of the decoder: catch, count, log at Debug.

### 8.3 Packets and fields consumed

Packet ids (verify against the spec): 0 Motion, 1 Session, 2 Lap Data, 3 Event, 4 Participants, 5 Car Setups, 6 Car Telemetry, 7 Car Status, 8 Final Classification, 9 Lobby Info, 10 Car Damage, 11 Session History, 12 Tyre Sets, 13 Motion Ex, 14 Time Trial, 15 Lap Positions.

- Session: weather, track and air temperature, total laps, track length, session type, track id, safety car status, ideal and latest pit window laps, session time left, pit speed limit, forecast samples (time offset, weather, temperatures, rain percentage), paused flag.
- Lap Data (all cars): last and current lap time, sector times, delta to car in front and to leader, lap distance, position, lap number, pit status, pit stops, sector, lap invalid flag, penalties, warnings, unserved drive-through and stop-go counts, grid position, driver status, result status, pit lane timers.
- Car Telemetry: speed, throttle, brake, steering, gear, RPM, DRS, brake temperatures, tyre surface and inner temperatures, tyre pressures, engine temperature.
- Car Status: fuel mix, brake bias, fuel in tank, capacity, fuel remaining laps, ERS store, mode, harvested and deployed energy this lap, DRS allowed, actual and visual tyre compound, tyre age in laps, FIA flag, traction control, ABS, pit limiter.
- Car Damage: tyre wear and damage, brake damage, wing, floor, diffuser and sidepod damage, DRS and ERS faults, gearbox, engine component wear, blown or seized engine.
- Participants: name, team id, race number, driver id, AI-controlled flag.
- Session History (arrives for one car at a time, cycling): lap history, best lap and sector laps, tyre stints.
- Tyre Sets: available sets with compound, wear, life span, lap delta, fitted index.
- Event: four-letter codes such as SSTA, SEND, FTLP, RTMT, DRSE, DRSD, CHQF, PENA, SPTP, OVTK, SCAR, COLL. Parse payloads for the ones that have them; log and ignore unknown codes.
- Motion: world X and Z of all cars (track map). Phase 2: Car Setups, Final Classification, Lap Positions.

### 8.4 StateStore

Holds Session, 22 car records, the last 50 events, and connection stats. Written only by the decoder thread, read by the snapshot builder through an atomically swapped immutable copy. For the player car, log one record per completed lap (lap time, sectors, compound, tyre wear per wheel, fuel in tank), captured when the lap number increments.

### 8.5 SnapshotBuilder

Emits two messages: `live` every 100 ms and `field` every 500 ms (schemas in section 9), plus an immediate `event` message for new events. Other cars get reduced data only.

### 8.6 Recorder and Replayer

- Recorder: writes raw datagrams to a .pwrec file: magic PWRC, version 1, start time UTC, then records of \[uint32 ms since start\]\[uint16 length\]\[bytes\]. Start and stop from the Host UI; auto-name by track, session type and time.
- Replayer: feeds a recording to the UDP port at original timing (speed 1x, 2x, 4x, optional loop). Available in the Sim tool and as a Replay mode in the Host.
- Synthetic generator in the Sim tool: a plausible 22-car, 5-lap race covering Session, Lap Data, Car Telemetry, Car Status, Car Damage, Participants, Tyre Sets, Session History, Event and Motion packets. Write minimal encoders from the spec and verify by round-tripping through the decoder.

## 9. Message schemas (PitWall.Contracts)

Every message: { type, seq, t, payload }, where t is host UTC milliseconds. Wheel arrays: RL, RR, FL, FR.

`live` payload (10 Hz):

- conn: state (waiting | receiving | stale), pps, packetFormat
- player: carIndex, position, lap, sector, currentLapMs, lastLapMs, bestLapMs, gapAheadMs, gapBehindMs, gapLeaderMs, pitStatus, invalidLap
- driver: speedKph, gear, rpm, maxRpm, throttle 0-1, brake 0-1, steer -1..1, drs (off | available | active)
- tyres: surfaceC\[4\], innerC\[4\], pressurePsi\[4\], brakeC\[4\], wearPct\[4\]
- energy: fuelKg, fuelLaps, fuelMix, ersPct, ersMode, ersHarvestedMJ, ersDeployedMJ (ERS max store is 4 MJ; the game reports joules)

`field` payload (2 Hz):

- session: uid, type, track, trackLengthM, totalLaps, currentLap, timeLeftS, weather, trackTempC, airTempC, safetyCar (none | sc | vsc | formation), flag, pitWindow {ideal, latest}, paused
- forecast\[\]: offsetMin, weather, trackTempC, airTempC, rainPct
- cars\[22\]: index, name, teamId, teamColor, raceNumber, position, gridPosition, gapLeaderMs, intervalMs, lastLapMs, bestLapMs, compound, tyreAgeLaps, pitStatus, stops, penaltiesS, driverStatus, resultStatus, isPlayer, isAi
- tyreInfo: compoundActual, compoundVisual, ageLaps, damagePct\[4\]
- damage: frontWingL, frontWingR, rearWing, floor, diffuser, sidepod, drsFault, ersFault, gearbox, engine components, engineBlown
- laps\[\]: player lap log (lapNo, lapMs, s1Ms, s2Ms, s3Ms, valid, compound, wearPct\[4\], fuelKg)
- strategy: stints\[\] (startLap, endLap, compound), tyreSets\[\] (compound, wearPct, lifeLeft, available, fitted)
- derived: see section 14
- events\[\]: last 50 events

`event` payload: id, code, lap, carIndex, otherCarIndex, text.

## 10. Local server and protocol

Kestrel binds 0.0.0.0:5080 (configurable).

- GET / engineer dashboard, GET /driver driver view, GET /setup LAN URL and QR code page.
- WS /ws?role=engineer|driver (LAN). Optional LAN PIN, off by default.
- /api/\* management endpoints (status, recording start and stop, room create and close, settings) accept loopback requests only; all others get 403.

Client to host messages: hello {role, name, clientVersion}, call {callId, preset, text}, callAck {callId}, ping. Host to client: live, field, event, call, callStatus, roomStatus, viewerList, pong, error. The relay uses the same message types, with the extra messages in section 11.

## 11. Remote sharing with a room code

Goal: a friend outside the LAN sees the dashboard and sends calls with no port forwarding, no install, and no account.

### 11.1 Parts

- Relay at a configurable base URL (example: https://pitwall-relay.example.com).
- Host connects outbound to wss://relay/ws/host. Engineers open https://relay/join and enter a code, or open a direct link https://relay/j/XXXX-XXXX.
- The relay serves the same static web client; the client picks LAN or relay transport from the page origin.

### 11.2 Flow

1. Host presses Share remotely. Host opens the host WebSocket and sends createRoom {clientVersion, hostName}.
2. Relay returns roomCreated {roomCode, hostSecret, joinUrl, expiresAt}. The room code is 8 characters from the alphabet ABCDEFGHJKMNPQRSTVWXYZ23456789 (30 symbols, about 39 bits), generated with RandomNumberGenerator and shown as XXXX-XXXX. The hostSecret is 32 random bytes, base64url, known only to the host.
3. Host shows the code, link, QR code and copy button.
4. Engineer opens the join page, types the code and a display name, and connects to wss://relay/ws/viewer?room=CODE, then sends hello {role: engineer, name}.
5. If approval is on (default), the relay notifies the host of a join request {requestId, name}; the host approves or denies. On approval the relay issues a viewerToken (random, in memory) so the viewer can reconnect without approval.
6. The relay forwards live, field and event messages from the host to approved viewers and routes call messages from engineers to the host only. The host fans calls out to its driver views.
7. Late joiners receive the cached last live and field messages immediately.

### 11.3 Room controls (host)

Close room, regenerate code (old code stops working, approved viewers stay unless kicked), kick viewer, require approval on or off, allow calls on or off, viewer list with names and connection state.

### 11.4 Security rules

- Production relay accepts wss and https only (ws allowed on localhost for development).
- Failed or unknown-code joins: at most 10 per IP per minute and 30 per hour, then HTTP 429. Not-found, expired and wrong-secret responses must look identical to prevent enumeration. Compare secrets in constant time.
- Room lifetime: closes 2 minutes after the host disconnects, or after 12 hours, or when the host closes it; viewers then receive roomClosed.
- Limits: 8 connections per room, 64 KB per message, host snapshots at most 20 messages per second, calls at most 5 per 10 seconds per viewer, call text at most 120 characters with control characters stripped. Calls are rendered as text, never HTML.
- Viewers may only send hello, call, callAck and ping; anything else closes the connection.
- The relay treats live and field payloads as opaque, writes nothing to disk, and logs only room creation and closure and connection counts (IP addresses hashed).
- Check the Origin header on browser connections against the relay's own origin.

### 11.5 Bandwidth and reconnect

Splitting live (about 2 KB) and field (about 15 KB) keeps uplink near 50 KB/s; enable permessage-deflate. Host reconnects with exponential backoff (1 s up to 30 s) using the hostSecret to resume the same room inside its lifetime; viewers reconnect with their viewerToken. The UI shows a Reconnecting state.

### 11.6 Host UI for sharing

A Share remotely panel with a large room code, copy link, QR code, relay connection status, viewer list with kick buttons, the toggles in 11.3, and a Close room button.

## 12. Engineer-to-driver calls

- Presets: BOX THIS LAP, BOX NEXT LAP, STAY OUT, PUSH NOW, MANAGE TYRES, SAVE FUEL, MAX ERS, CAR BEHIND CLOSING, GOOD LAP, plus free text up to 120 characters.
- Delivery: the Host validates the call and sends it to all driver views and the Host UI, and optionally plays a sound and speaks it on the Host PC (NAudio plus Windows speech, selectable output device).
- Driver view: full-screen coloured banner with large text, an alert tone, vibration and optional speech synthesis. Browsers block audio until a user gesture, so show an Enable sound button on first load. A Got it button sends callAck. BOX calls stay until acknowledged or 20 seconds; other calls dismiss after 10 seconds. Keep the screen awake with the Wake Lock API.
- Driver view also shows lap, position, tyre compound and age, fuel laps, gap ahead and behind. Dark, high contrast, readable from a distance.
- Engineer UI shows call state: sent, delivered (at least one driver view received it), acknowledged. One call per second maximum, queue of at most 3.

## 13. Dashboard specification

Dark theme, responsive widget grid. Header: session type, track, lap x of y, time left, weather, track and air temperature, safety car state, connection state, latency.

MVP widgets:

1. Timing tower: position, name, team colour, gap to leader, interval, last and best lap, compound and age, pit status and stops, penalties, position change vs grid; highlight the player.
2. Driver card: speed, gear, RPM bar, throttle, brake, steering, DRS.
3. Tyres: four-corner diagram with wear %, surface and inner temperature, pressure, colour thresholds, compound badge, age, predicted laps left.
4. Fuel and ERS: fuel kg, laps remaining, burn per lap, surplus or deficit in laps, mix; ERS store, mode, harvested and deployed.
5. Gaps ahead and behind with trend arrows.
6. Lap table: last 10 laps with sectors, validity, compound.
7. Damage panel with component bars and DRS or ERS faults.
8. Weather panel: current conditions and a 60-minute forecast with rain percentage.
9. Events feed.
10. Call panel: presets, text box, call status log.

Phase 2: track map from motion data, strategy planner (pit window, stints, tyre set inventory, pit-loss and undercut helper), lap time chart, tyre wear per wheel by lap, position chart from Lap Positions, brake temperatures, setup viewer, session summary and CSV export, units and theme settings, saved layouts, colour-blind palette.

Alert rules (configurable, toast plus colour): tyre wear above 70% amber and 85% red; tyre surface temperature outside an adjustable range; fuel laps remaining below laps left plus 1; any aero damage above 25%; DRS or ERS fault; new penalty; safety car or VSC; rain probability above 50% within 10 minutes; gap behind under 1.0 s for 3 laps.

## 14. Derived metrics

- Laps remaining: totalLaps - currentLap + 1 in a race, refined with lapDistance / trackLength.
- Tyre wear rate per wheel: mean wear gain over the last 3 completed laps of the stint (at least 2 laps, else not available). Laps to threshold T (default 80%) = (T - wear) / rate.
- Fuel burn per lap: mean of the last 3 completed laps, excluding lap 1 and safety car or VSC laps. Fuel surplus in laps = fuelKg / burn - laps remaining; also show the game's own fuel remaining laps.
- Gap trend: change in gap ahead and behind per lap over the last 3 laps (negative means closing).
- Pit loss: default 22 s, configurable per track, replaced by the running average of measured stops from pit lane timers.
- Undercut hint (experimental, labelled as such): flag when the gap ahead is under 2.0 s and the own tyre age is at least 3 laps above that car's tyre age.

## 15. Non-functional requirements

- Latency from datagram to a LAN browser: p95 under 150 ms.
- Host under 10% CPU on a mid-range laptop at 60 Hz and under 300 MB memory. Relay under 100 MB with 20 rooms.
- Resilience: game restarts are handled through session UID changes; any component can restart without restarting the others.
- Logging: Serilog, rolling daily files in %LOCALAPPDATA%/PitWall/logs, 7 days kept.
- Licence MIT. No EA, F1 or team logos or trademarks; team colours as hex only; state that the project is unaffiliated. No analytics or phone-home.
- Packaging: Host as a self-contained win-x64 build with an installer (Phase 3); Relay with a Dockerfile and an example fly.toml.

## 16. Testing

- Unit: decoder against captured fixtures, state merges, derived metrics, room code charset and uniqueness, rate limiter, protocol round-trips.
- Integration: Sim replay into Host with a WebSocket test client asserting snapshot content; Relay tests with a fake host and two viewers (join, approval, kick, reconnect, call routing, rate limits, TTL).
- Web: Vitest for selectors and formulas; Playwright smoke test of the join flow against a mocked relay.
- Manual: real-PS5 checklist in docs/testing.md.

## 17. Milestones and acceptance

- M0 Skeleton: solution builds, CI (GitHub Actions) runs build and tests, Contracts defined.
- M1 POC listener: host UI or console shows packets per second and decoded player telemetry from the PS5 or Sim; recorder and replayer work. Accept: replaying a recording reproduces the same values.
- M2 State and local dashboard: state store, snapshot builder, Kestrel, WebSocket, MVP widgets 1-4. Accept: open http://PC-IP:5080 on a phone and see live data.
- M3 Full MVP and calls: widgets 5-10, driver view, audio, alerts.
- M4 Relay and room codes: relay, host share panel, join page, approval, kick, reconnect, rate limits, Docker. Accept: a friend outside the network joins with a code; a BOX call reaches the driver view in under 1 s.
- M5 Phase 2 widgets, polish, installer.

Each milestone must run, be tested, be documented, and be committed separately.

## 18. Rules for the coding agent

- Follow section 4. Ask the owner only when blocked; otherwise choose sensible defaults and record them in docs/decisions.md.
- Never invent byte offsets or enum values; take them from F1Game.UDP or the official spec, and test with captured packets.
- Keep contracts in PitWall.Contracts and generate the TypeScript types from them with a script so both sides stay in sync.
- No secrets in the repo; the relay URL is configuration.
- Treat all network input as hostile: validate, bound sizes, and never let one bad packet or message crash a loop.
- Give each component a README with run commands (dotnet run for Host, npm run dev for Web, dotnet run --project tools/PitWall.Sim for the simulator).

## 19. References

- EA SPORTS F1 25 UDP Specification (EA forums, F1 Games Game Info Hub): https://forums.ea.com/blog/f1-games-game-info-hub-en/ea-sports%E2%84%A2-f1%C2%AE25-udp-specification/12187347
- F1Game.UDP NuGet package: https://www.nuget.org/packages/F1Game.UDP
- f1\_game\_library\_models\_25 (Rust) on docs.rs, a second implementation to cross-check field layouts.
