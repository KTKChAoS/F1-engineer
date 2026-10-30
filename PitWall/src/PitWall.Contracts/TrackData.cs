namespace PitWall.Contracts;

/// <summary>
/// Track ID → name mapping from the F1 25 spec.
/// Hex-only team colours (no logos/trademarks per spec §15).
/// </summary>
public static class TrackData
{
    public static readonly IReadOnlyDictionary<int, string> TrackNames = new Dictionary<int, string>
    {
        [0]  = "Melbourne",
        [1]  = "Sepang",
        [2]  = "Shanghai",
        [3]  = "Sakhir",
        [4]  = "Catalunya",
        [5]  = "Monaco",
        [6]  = "Montreal",
        [7]  = "Silverstone",
        [8]  = "Hockenheim",
        [9]  = "Hungaroring",
        [10] = "Spa",
        [11] = "Monza",
        [12] = "Singapore",
        [13] = "Suzuka",
        [14] = "Abu Dhabi",
        [15] = "Austin",
        [16] = "Interlagos",
        [17] = "Red Bull Ring",
        [18] = "Sochi",
        [19] = "Mexico City",
        [20] = "Baku",
        [21] = "Sakhir Short",
        [22] = "Silverstone Short",
        [23] = "Austin Short",
        [24] = "Suzuka Short",
        [25] = "Hanoi",
        [26] = "Zandvoort",
        [27] = "Imola",
        [28] = "Portimão",
        [29] = "Jeddah",
        [30] = "Miami",
        [31] = "Las Vegas",
        [32] = "Losail",
        [33] = "Lusail Short",
        [34] = "Shanghai Short",
    };

    public static string GetTrackName(int trackId)
        => TrackNames.TryGetValue(trackId, out var name) ? name : $"Track {trackId}";
}

/// <summary>
/// Team ID → hex colour. No logos or trademarks (MIT project, unaffiliated).
/// Colours approximate the 2025 liveries.
/// </summary>
public static class TeamColors
{
    public static readonly IReadOnlyDictionary<int, string> Colors = new Dictionary<int, string>
    {
        [0]  = "#3671C6",  // Red Bull
        [1]  = "#E8002D",  // Ferrari
        [2]  = "#6CD3BF",  // Mercedes
        [3]  = "#FF8000",  // McLaren
        [4]  = "#2293D1",  // Alpine
        [5]  = "#64C4FF",  // RB (VCARB)
        [6]  = "#52E252",  // Aston Martin
        [7]  = "#B6BABD",  // Williams
        [8]  = "#A6051A",  // Sauber
        [9]  = "#FFFFFF",  // Haas
    };

    public static string GetColor(int teamId)
        => Colors.TryGetValue(teamId, out var c) ? c : "#888888";
}
