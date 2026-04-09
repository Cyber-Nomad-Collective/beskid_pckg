using Server.Data;

namespace Server.Services;

public static class PackageHealthScoring
{
    public static PackageHealthStatus Calculate(PackageEntity package, double averageDownloads, double averageRating, int reviewCount)
    {
        var now = DateTimeOffset.UtcNow;
        var daysSinceUpdate = Math.Max(0d, (now - package.UpdatedAtUtc).TotalDays);
        var downloadRatio = averageDownloads <= 0 ? 1d : package.TotalDownloads / averageDownloads;

        var updateState = BuildUpdateRateState(daysSinceUpdate);
        var downloadState = BuildDownloadState(downloadRatio);
        var reviewState = BuildReviewState(averageRating, reviewCount);

        var score = StatusScoreBuilder
            .Create()
            .Add(updateState.Weight, updateState.Normalized)
            .Add(downloadState.Weight, downloadState.Normalized)
            .Add(reviewState.Weight, reviewState.Normalized)
            .Build();

        var overall = score switch
        {
            >= 0.85 => ("thriving", "outstanding"),
            >= 0.68 => ("rising", "strong"),
            >= 0.48 => ("steady", "maintained"),
            _ => ("at-risk", "watchlist")
        };

        return new PackageHealthStatus(overall.Item1, overall.Item2, score, updateState, downloadState, reviewState);
    }

    private static FactorStatus BuildUpdateRateState(double daysSinceUpdate) => daysSinceUpdate switch
    {
        <= 2 => new FactorStatus("update-rate", "fast-track", "blazing", 1d, 0.42),
        <= 7 => new FactorStatus("update-rate", "fast-track", "surging", 0.93, 0.42),
        <= 14 => new FactorStatus("update-rate", "active", "rapid", 0.82, 0.42),
        <= 30 => new FactorStatus("update-rate", "active", "warm", 0.72, 0.42),
        <= 60 => new FactorStatus("update-rate", "stable", "steady", 0.6, 0.42),
        <= 120 => new FactorStatus("update-rate", "stable", "cool", 0.5, 0.42),
        <= 240 => new FactorStatus("update-rate", "stale", "aging", 0.36, 0.42),
        _ => new FactorStatus("update-rate", "stale", "dormant", 0.2, 0.42)
    };

    private static FactorStatus BuildDownloadState(double ratio) => ratio switch
    {
        < 0.25 => new FactorStatus("downloads", "underdog", "emerging", 0.34, 0.35),
        < 0.5 => new FactorStatus("downloads", "underdog", "rising", 0.46, 0.35),
        < 0.85 => new FactorStatus("downloads", "mainstream", "steady", 0.58, 0.35),
        < 1.25 => new FactorStatus("downloads", "mainstream", "solid", 0.7, 0.35),
        < 2.0 => new FactorStatus("downloads", "popular", "trending", 0.83, 0.35),
        _ => new FactorStatus("downloads", "popular", "hot", 0.95, 0.35)
    };

    private static FactorStatus BuildReviewState(double avg, int count)
    {
        if (count == 0)
        {
            return new FactorStatus("reviews", "nascent", "unreviewed", 0.4, 0.23);
        }

        if (avg >= 4.6)
        {
            return new FactorStatus("reviews", "trusted", count >= 20 ? "beloved" : "praised", 0.94, 0.23);
        }

        if (avg >= 4.0)
        {
            return new FactorStatus("reviews", "trusted", count >= 8 ? "well-reviewed" : "promising", 0.82, 0.23);
        }

        if (avg >= 3.0)
        {
            return new FactorStatus("reviews", "mixed", "in-progress", 0.63, 0.23);
        }

        return new FactorStatus("reviews", "warning", "critical", 0.34, 0.23);
    }

    private sealed class StatusScoreBuilder
    {
        private double _weighted;
        private double _weightTotal;

        private StatusScoreBuilder()
        {
        }

        public static StatusScoreBuilder Create() => new();

        public StatusScoreBuilder Add(double weight, double normalized)
        {
            var clampedWeight = Math.Max(0, weight);
            var clampedNormalized = Math.Clamp(normalized, 0, 1);
            _weighted += clampedWeight * clampedNormalized;
            _weightTotal += clampedWeight;
            return this;
        }

        public double Build() => _weightTotal <= 0 ? 0 : _weighted / _weightTotal;
    }
}

public sealed record PackageHealthStatus(
    string State,
    string SubState,
    double Score,
    FactorStatus UpdateRate,
    FactorStatus Downloads,
    FactorStatus Reviews);

public sealed record FactorStatus(
    string Factor,
    string State,
    string SubState,
    double Normalized,
    double Weight);
