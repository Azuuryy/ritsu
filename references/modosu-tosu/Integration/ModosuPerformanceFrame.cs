using System;

namespace osu.Game.Rulesets.Modosu.Integration
{
    /// <summary>
    /// Immutable values produced by the game's calculators for one gameplay session.
    /// </summary>
    public sealed record ModosuPerformanceFrame
    {
        public int Schema { get; init; } = 1;
        public int ProcessId { get; init; }
        public long ProcessStartTime { get; init; }
        public string SessionId { get; init; } = string.Empty;
        public string BeatmapHash { get; init; } = string.Empty;
        public string ModsJson { get; init; } = "[]";
        public string Phase { get; init; } = "play";
        public long UpdatedAt { get; init; }
        public double Time { get; init; }
        public int Retries { get; init; }
        public string Date { get; init; } = string.Empty;
        public long Score { get; init; }
        public int MaxCombo { get; init; }
        public int MaximumCombo { get; init; }
        public ModosuPerformanceHits Hits { get; init; } = new();
        public double? Pp { get; init; }
        public double? MaximumPp { get; init; }
        public double? FcPp { get; init; }
        public double? MaxAchievablePp { get; init; }
        public double? LiveStars { get; init; }
        public double? TotalStars { get; init; }
        public ModosuPerformanceDetails? Details { get; init; }
        public ModosuPerformanceDetails? FcDetails { get; init; }

        public static double? Finite(double? value) => value is >= 0 && double.IsFinite(value.Value) ? value : null;
    }

    public sealed record ModosuPerformanceHits
    {
        public int Great { get; init; }
        public int Ok { get; init; }
        public int Meh { get; init; }
        public int Miss { get; init; }
    }

    public sealed record ModosuPerformanceDetails
    {
        public double Aim { get; init; }
        public double Speed { get; init; }
        public double Accuracy { get; init; }
        public double Flashlight { get; init; }
    }
}
