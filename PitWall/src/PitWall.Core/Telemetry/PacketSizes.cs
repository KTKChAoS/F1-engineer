namespace PitWall.Core.Telemetry;

/// <summary>
/// F1 25 UDP packet sizes from the official telemetry structures document.
/// </summary>
public static class PacketSizes
{
    public const int Header = 29;
    public const int Motion = 1349;
    public const int Session = 753;
    public const int Lap = 1285;
    public const int Event = 45;
    public const int Participants = 1284;
    public const int CarSetups = 1133;
    public const int CarTelemetry = 1352;
    public const int CarStatus = 1239;
    public const int FinalClassification = 1042;
    public const int LobbyInfo = 954;
    public const int CarDamage = 1041;
    public const int SessionHistory = 1460;
    public const int TyreSets = 231;
    public const int MotionEx = 273;
    public const int TimeTrial = 101;
    public const int LapPositions = 1131;

    public const int Cars = 22;
    public const int MarshalZones = 21;
    public const int WeatherForecastSamples = 64;
    public const int SessionsInWeekend = 12;
    public const int ParticipantNameLength = 32;
    public const int EventCodeLength = 4;
    public const int EventDetailsLength = 12;
}
