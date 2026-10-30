namespace PitWall.Contracts;

/// <summary>
/// Shared constants used across PitWall components.
/// </summary>
public static class Constants
{
    // ── Network ──────────────────────────────────────────────────────
    public const int PacketFormat2025 = 2025;
    public const int PacketHeaderSize = 29;

    public const int DefaultUdpPort = 20777;
    public const int DefaultWebPort = 5080;
    public const int UdpChannelCapacity = 1024;

    // ── Timing ───────────────────────────────────────────────────────
    public const int LiveSnapshotIntervalMs = 100;   // 10 Hz
    public const int FieldSnapshotIntervalMs = 500;  // 2 Hz
    public const int ConnectionStaleThresholdMs = 3000;

    // ── Grid ─────────────────────────────────────────────────────────
    public const int MaxCars = 22;

    // ── Wheel order: RL, RR, FL, FR (as per EA spec) ─────────────────
    public const int WheelRL = 0;
    public const int WheelRR = 1;
    public const int WheelFL = 2;
    public const int WheelFR = 3;
    public const int WheelCount = 4;

    // ── Recorder ─────────────────────────────────────────────────────
    public const string RecorderMagic = "PWRC";
    public const byte RecorderVersion = 1;

    // ── Room codes ───────────────────────────────────────────────────
    /// <summary>
    /// 30-symbol alphabet for room codes (~39 bits of entropy for 8 chars).
    /// Excludes visually ambiguous characters (0, O, I, L, 1, U).
    /// </summary>
    public const string RoomCodeAlphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";
    public const int RoomCodeLength = 8;
    public const int RoomCodeHalfLength = 4;

    // ── Rate limits ──────────────────────────────────────────────────
    public const int MaxViewersPerRoom = 8;
    public const int MaxMessageSizeBytes = 64 * 1024;  // 64 KB
    public const int MaxHostSnapshotsPerSecond = 20;
    public const int MaxCallsPer10Seconds = 5;
    public const int MaxCallTextLength = 120;
    public const int MaxEventsStored = 50;

    // ── Room lifetime ────────────────────────────────────────────────
    public const int RoomGracePeriodSeconds = 120;     // 2 min after host disconnect
    public const int RoomMaxLifetimeHours = 12;

    // ── Reconnect ────────────────────────────────────────────────────
    public const int ReconnectMinDelayMs = 1000;
    public const int ReconnectMaxDelayMs = 30000;

    // ── Call timeouts ────────────────────────────────────────────────
    public const int BoxCallDismissMs = 20_000;
    public const int DefaultCallDismissMs = 10_000;

    // ── Alert thresholds (defaults, configurable) ────────────────────
    public const float TyreWearAmberPct = 70f;
    public const float TyreWearRedPct = 85f;
    public const float AeroDamageAlertPct = 25f;
    public const float RainProbAlertPct = 50f;
    public const float GapBehindAlertSeconds = 1.0f;
    public const int GapBehindAlertLaps = 3;

    // ── Derived metrics ──────────────────────────────────────────────
    public const float DefaultPitLossSeconds = 22f;
    public const float DefaultTyreWearThresholdPct = 80f;
    public const float UndercutGapThresholdSeconds = 2.0f;
    public const int UndercutTyreAgeDeltaLaps = 3;
    public const int DerivedSampleLaps = 3;  // look-back window
    public const float ErsMaxStoreMJ = 4.0f;

    // ── Logging ──────────────────────────────────────────────────────
    public const int LogRetainedDays = 7;
    public const string AppDataFolderName = "PitWall";
}
