using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace McIntoshHotshots.Model;

/// <summary>
/// Player statistics for a specific time period
/// Used for time series analysis and trend visualization
/// </summary>
public class PlayerStatistics
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // Core Statistics
    public int MatchesPlayed { get; set; }
    public int MatchesWon { get; set; }
    public int MatchesLost { get; set; }
    public decimal WinRate { get; set; }

    // Scoring Metrics
    public decimal AverageScore { get; set; }
    public decimal MedianScore { get; set; }
    public decimal HighestScore { get; set; }
    public decimal LowestScore { get; set; }

    // Advanced Metrics (from existing system)
    public decimal CheckoutPercentage { get; set; }
    public decimal DoublesHitRate { get; set; }
    public decimal CompletionRate { get; set; }

    // Leg Statistics
    public int TotalLegs { get; set; }
    public int LegsWon { get; set; }
    public decimal AverageDartsPerLeg { get; set; }

    // Tournament Performance
    public int TournamentsEntered { get; set; }
    public decimal? AveragePlacement { get; set; }

    // Elo Rating (if tracked during period)
    public decimal? EloRating { get; set; }
    public decimal? EloChange { get; set; }
}

/// <summary>
/// Tournament-level statistics
/// Aggregate stats for all players/matches in a tournament
/// </summary>
public class TournamentStatistics
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public DateTime TournamentDate { get; set; }

    // Participation
    public int TotalPlayers { get; set; }
    public int TotalMatches { get; set; }
    public int TotalLegs { get; set; }

    // Scoring Metrics
    public decimal AverageScore { get; set; }
    public decimal MedianScore { get; set; }
    public decimal HighestScore { get; set; }
    public decimal LowestScore { get; set; }

    // Performance Metrics
    public decimal AverageCheckoutPercentage { get; set; }
    public decimal AverageDoublesHitRate { get; set; }
    public decimal AverageCompletionRate { get; set; }
    public decimal AverageDartsPerLeg { get; set; }

    // Tournament Winner Info
    public int? WinnerId { get; set; }
    public string? WinnerName { get; set; }

    // Time Metrics
    public TimeSpan? AverageMatchDuration { get; set; }
    public TimeSpan? TotalPlayTime { get; set; }
}

/// <summary>
/// Head-to-head comparison between two players
/// Used for matchup analysis and rivalry tracking
/// </summary>
public class HeadToHeadRecord
{
    public int Player1Id { get; set; }
    public string Player1Name { get; set; } = string.Empty;

    public int Player2Id { get; set; }
    public string Player2Name { get; set; } = string.Empty;

    // Overall Record
    public int TotalMatches { get; set; }
    public int Player1Wins { get; set; }
    public int Player2Wins { get; set; }
    public int Draws { get; set; }

    // Leg Statistics
    public int TotalLegs { get; set; }
    public int Player1LegsWon { get; set; }
    public int Player2LegsWon { get; set; }

    // Performance Comparison
    public decimal Player1AverageScore { get; set; }
    public decimal Player2AverageScore { get; set; }

    public decimal Player1CheckoutPercentage { get; set; }
    public decimal Player2CheckoutPercentage { get; set; }

    public decimal Player1DoublesHitRate { get; set; }
    public decimal Player2DoublesHitRate { get; set; }

    // Streak Information
    public int CurrentStreak { get; set; } // Positive = Player1, Negative = Player2
    public int Player1LongestWinStreak { get; set; }
    public int Player2LongestWinStreak { get; set; }

    // Most Recent Match
    public DateTime? LastMatchDate { get; set; }
    public int? LastMatchWinnerId { get; set; }

    // Historical Data
    public DateTime FirstMatchDate { get; set; }
}

/// <summary>
/// Time series data point for charting
/// Generic structure for various metric types over time
/// </summary>
public class TimeSeriesDataPoint
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Chart.js formatted data structure
/// Ready for direct consumption by the JavaScript interop
/// </summary>
public class ChartDataset
{
    public string Label { get; set; } = string.Empty;
    public List<decimal> Data { get; set; } = new();
    public string BorderColor { get; set; } = string.Empty;
    public string BackgroundColor { get; set; } = string.Empty;
    public int BorderWidth { get; set; } = 2;
    public bool Fill { get; set; } = false;
    public string? Tension { get; set; }
}

/// <summary>
/// Complete chart configuration for Chart.js
/// </summary>
public class ChartConfiguration
{
    public string Type { get; set; } = "line"; // line, bar, radar, etc.
    public List<string> Labels { get; set; } = new();
    public List<ChartDataset> Datasets { get; set; } = new();
    public Dictionary<string, object>? Options { get; set; }
}
