# Data Model: Advanced Stat Viewing System

## Core Entities

### PlayerStatistics
**Purpose**: Historical performance data for individual players over time

```csharp
public class PlayerStatistics
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public DateTime GameDate { get; set; }
    public decimal Score { get; set; }
    public decimal CompletionRate { get; set; }          // Percentage of games completed
    public decimal CheckoutPercentage { get; set; }      // Successful checkout rate
    public decimal DoublesHitRate { get; set; }          // Doubles accuracy rate
    public int? TournamentId { get; set; }              // Nullable for practice games
    public string GameType { get; set; }                // "501", "301", "Cricket", etc.
    public TimeSpan GameDuration { get; set; }
    public int ThrowCount { get; set; }                 // Total throws in game

    // Navigation properties
    public Player Player { get; set; }
    public Tournament Tournament { get; set; }
}
```

**Validation Rules**:
- Score must be between 0 and 501 for standard games
- CompletionRate, CheckoutPercentage, DoublesHitRate must be between 0 and 1
- GameDate cannot be in the future
- ThrowCount must be positive

### TournamentStatistics
**Purpose**: Aggregate tournament data and player rankings within tournaments

```csharp
public class TournamentStatistics
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public DateTime CalculationDate { get; set; }        // When stats were calculated
    public decimal AverageScore { get; set; }
    public decimal MedianScore { get; set; }
    public int ParticipantCount { get; set; }
    public decimal CompletionRate { get; set; }          // Tournament completion rate
    public string TournamentType { get; set; }           // "League", "Knockout", "Round Robin"
    public decimal AverageGameDuration { get; set; }     // In minutes
    public int TotalGamesPlayed { get; set; }

    // Navigation properties
    public Tournament Tournament { get; set; }
}
```

**Validation Rules**:
- ParticipantCount must be positive
- AverageScore and MedianScore must be positive
- CompletionRate must be between 0 and 1
- CalculationDate cannot be in the future

### HeadToHeadRecord
**Purpose**: Match history between specific players for comparative analysis

```csharp
public class HeadToHeadRecord
{
    public int Id { get; set; }
    public int Player1Id { get; set; }
    public int Player2Id { get; set; }
    public DateTime MatchDate { get; set; }
    public decimal Player1Score { get; set; }
    public decimal Player2Score { get; set; }
    public int WinnerId { get; set; }                    // Player1Id or Player2Id
    public string TournamentType { get; set; }
    public int? TournamentId { get; set; }              // Nullable for practice matches
    public TimeSpan MatchDuration { get; set; }
    public string GameFormat { get; set; }              // "Best of 3", "Best of 5", etc.

    // Navigation properties
    public Player Player1 { get; set; }
    public Player Player2 { get; set; }
    public Player Winner { get; set; }
    public Tournament Tournament { get; set; }
}
```

**Validation Rules**:
- Player1Id and Player2Id must be different
- WinnerId must be either Player1Id or Player2Id
- Player1Score and Player2Score must be positive
- MatchDate cannot be in the future

### TimeSeriesDataPoint
**Purpose**: Pre-aggregated time series data for efficient chart rendering

```csharp
public class TimeSeriesDataPoint
{
    public int Id { get; set; }
    public string EntityType { get; set; }              // "Player", "Tournament"
    public int EntityId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Period { get; set; }                  // "weekly", "monthly", "quarterly", "yearly"
    public string MetricType { get; set; }              // "average_score", "median_score", "completion_rate"
    public decimal Value { get; set; }
    public int DataPointCount { get; set; }             // Number of source records
    public DateTime CalculatedAt { get; set; }

    // Statistical metadata
    public decimal? StandardDeviation { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
}
```

**Validation Rules**:
- EntityType must be "Player" or "Tournament"
- Period must be one of: "weekly", "monthly", "quarterly", "yearly"
- PeriodEnd must be after PeriodStart
- DataPointCount must be at least 1
- Value must be positive for score metrics

### MetricCalculation
**Purpose**: Cached calculation results for complex statistical operations

```csharp
public class MetricCalculation
{
    public int Id { get; set; }
    public string CalculationType { get; set; }         // "head_to_head", "trend_analysis", "percentile"
    public string Parameters { get; set; }              // JSON serialized parameters
    public string Result { get; set; }                  // JSON serialized result
    public DateTime CalculatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string CacheKey { get; set; }                // Unique identifier for caching
}
```

## Existing Entity Extensions

### Player (Extended)
```csharp
public class Player // Existing entity
{
    // Existing properties...

    // New navigation properties for statistical views
    public ICollection<PlayerStatistics> Statistics { get; set; }
    public ICollection<HeadToHeadRecord> Player1Matches { get; set; }
    public ICollection<HeadToHeadRecord> Player2Matches { get; set; }
    public ICollection<HeadToHeadRecord> WonMatches { get; set; }
}
```

### Tournament (Extended)
```csharp
public class Tournament // Existing entity
{
    // Existing properties...

    // New navigation properties for statistical views
    public ICollection<TournamentStatistics> Statistics { get; set; }
    public ICollection<PlayerStatistics> PlayerStatistics { get; set; }
    public ICollection<HeadToHeadRecord> HeadToHeadRecords { get; set; }
}
```

## Database Indexes

### Performance Optimization Indexes
```sql
-- Time series queries
CREATE INDEX IX_PlayerStatistics_PlayerId_GameDate
ON PlayerStatistics (PlayerId, GameDate);

CREATE INDEX IX_PlayerStatistics_TournamentId_GameDate
ON PlayerStatistics (TournamentId, GameDate);

-- Head-to-head lookups
CREATE INDEX IX_HeadToHeadRecord_Players_Date
ON HeadToHeadRecord (Player1Id, Player2Id, MatchDate);

-- Time series data points
CREATE INDEX IX_TimeSeriesDataPoint_Entity_Period
ON TimeSeriesDataPoint (EntityType, EntityId, Period, PeriodStart);

-- Metric calculations cache
CREATE UNIQUE INDEX IX_MetricCalculation_CacheKey
ON MetricCalculation (CacheKey);
```

## State Transitions

### TimeSeriesDataPoint Lifecycle
1. **Raw Data Collection**: PlayerStatistics records created during game play
2. **Aggregation Trigger**: Nightly/hourly batch job or on-demand calculation
3. **Calculation**: Statistical aggregation by period (weekly/monthly/quarterly/yearly)
4. **Storage**: TimeSeriesDataPoint records created/updated
5. **Expiration**: Recalculation when source data changes

### MetricCalculation Caching
1. **Request**: User requests complex statistical calculation
2. **Cache Check**: Look for existing non-expired calculation
3. **Calculate**: If not cached, perform calculation and store result
4. **Serve**: Return cached or fresh calculation result
5. **Expire**: Remove expired calculations via background job

## Data Volume Estimates

| Entity | Current Volume | Growth Rate | Storage Impact |
|--------|---------------|-------------|----------------|
| PlayerStatistics | ~500k records | +50k/month | Primary growth driver |
| TournamentStatistics | ~1k records | +100/month | Minimal impact |
| HeadToHeadRecord | ~100k records | +10k/month | Moderate growth |
| TimeSeriesDataPoint | ~10k records | +2k/month | Controlled growth |
| MetricCalculation | ~1k records | Variable | Cache only, auto-expires |

## Migration Strategy

### Phase 1: Core Entities
1. Create PlayerStatistics table
2. Create TournamentStatistics table
3. Create HeadToHeadRecord table
4. Add basic indexes

### Phase 2: Performance Optimization
1. Create TimeSeriesDataPoint table
2. Create MetricCalculation table
3. Add performance indexes
4. Implement aggregation jobs

### Phase 3: Data Population
1. Migrate existing game data to PlayerStatistics
2. Calculate initial tournament statistics
3. Generate head-to-head records from match history
4. Pre-calculate time series data for better performance