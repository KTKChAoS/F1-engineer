# PitWall — Technology Decisions

*Record any deviation from the spec or design choices here.*

## D001 — .NET Version

**Decision:** Use .NET 10 LTS (`net10.0`; SDK 10.0.403 was used for M0 verification)
**Reason:** This matches the fixed runtime decision in spec §4. The local machine initially had only .NET 8 and 9; the .NET 10 SDK was installed temporarily to build and verify the solution. .NET 9 is not used as the target framework.
**Date:** 2026-10-30

## D002 — F1Game.UDP Version

**Decision:** Pinned to F1Game.UDP 25.1.1
**Reason:** The spec targets F1 25 (UDP Format 2025). Version 25.x of the library matches. Version 26.0.0 was available but targets the 2026 Season Pack layouts. Using 25.1.1 as the latest patch for the 25 series.
**Date:** 2026-10-29

## D003 — WPF Template

**Decision:** Created Host as a WPF project targeting `net10.0-windows`
**Reason:** Per spec §4, the Host is a WPF control-panel window with embedded Kestrel. The target framework is `net10.0-windows` to match D001.
**Date:** 2026-10-29
