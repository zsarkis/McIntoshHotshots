# Quickstart: Advanced Stat Viewing System

## Overview
This quickstart guide validates the implementation of the Advanced Stat Viewing System by walking through key user scenarios and testing all major functionality.

## Prerequisites
- Blazor Server application running
- PostgreSQL database with sample data
- Chart.js integration working
- At least 3 players with historical game data

## Test Scenarios

### Scenario 1: Player Time Series View
**Objective**: Verify individual player statistics display with time series charts

**Steps**:
1. Navigate to `/players/{playerId}/stats`
2. Verify time series chart loads with default weekly view
3. Test metric toggle between Average and Median scores
4. Test period selector (weekly → monthly → quarterly → yearly)
5. Test date range selection functionality
6. Test tournament type filtering

**Expected Results**:
- ✅ Chart displays with minimum 3 data points (or shows appropriate message)
- ✅ Toggle smoothly switches between average and median values
- ✅ Period changes update chart granularity correctly
- ✅ Date range filtering works without errors
- ✅ Tournament filtering shows filtered data
- ✅ All current extra metrics preserved (completion rates, checkout %, doubles hit rate)

**Test Data Requirements**:
```
Player: John Doe (ID: 1)
- 15+ games over 3+ months
- Variety of tournament types
- Mix of scores (200-450 range)
```

### Scenario 2: Tournament Statistics View
**Objective**: Verify tournament-level statistical analysis

**Steps**:
1. Navigate to `/tournaments/{tournamentId}/stats`
2. Verify tournament time series chart loads
3. Test participant trend analysis
4. Verify average score evolution over tournament
5. Test completion rate tracking

**Expected Results**:
- ✅ Tournament statistics display correctly
- ✅ Participant count trends show over time
- ✅ Score distributions are accurate
- ✅ Completion rates match actual data

**Test Data Requirements**:
```
Tournament: Summer League 2025 (ID: 1)
- 10+ rounds/weeks of data
- 20+ participants
- Various game completion rates
```

### Scenario 3: Head-to-Head Comparison
**Objective**: Verify player vs player comparison functionality

**Steps**:
1. Navigate to `/stats/headtohead/{player1Id}/{player2Id}`
2. Verify overall win/loss ratio displays
3. Test recent form analysis (last 10 games)
4. Verify score differential trends
5. Test tournament type breakdown
6. Test edge case: players with no matches

**Expected Results**:
- ✅ Win/loss ratio accurately calculated
- ✅ Recent form shows correctly
- ✅ Average score differential is accurate
- ✅ Tournament breakdown shows by type
- ✅ Graceful handling when no matches exist
- ✅ Performance trends display correctly

**Test Data Requirements**:
```
Player 1: John Doe (ID: 1)
Player 2: Jane Smith (ID: 2)
- 15+ head-to-head matches
- Variety of tournament types
- Recent matches (within last month)
```

### Scenario 4: Interactive Chart Features
**Objective**: Verify Chart.js integration and interactive features

**Steps**:
1. Load any statistical view with time series chart
2. Test zoom functionality (mouse wheel or touch)
3. Test pan functionality (click and drag)
4. Test data point hover tooltips
5. Test chart legend interactions
6. Test responsive behavior (resize window)

**Expected Results**:
- ✅ Zoom in/out works smoothly
- ✅ Pan functionality responds correctly
- ✅ Tooltips show accurate data
- ✅ Legend toggles data series visibility
- ✅ Chart responds to window resize
- ✅ Performance remains smooth with large datasets

### Scenario 5: Data Validation and Edge Cases
**Objective**: Verify error handling and data validation

**Steps**:
1. Test with player having <3 statistical data points
2. Test with invalid player ID
3. Test with invalid date ranges
4. Test with very large date ranges (>2 years)
5. Test with no tournament data
6. Test network connectivity issues

**Expected Results**:
- ✅ Insufficient data shows appropriate message
- ✅ Invalid IDs return 404 with user-friendly message
- ✅ Invalid date ranges show validation errors
- ✅ Large date ranges show performance warnings or pagination
- ✅ Missing data gracefully handled
- ✅ Network errors show retry options

## Performance Validation

### Load Testing
**Target**: Support 1000+ concurrent users viewing statistics

**Test Procedure**:
1. Use load testing tool (e.g., NBomber, k6)
2. Simulate 1000 concurrent users
3. Mix of player stats, tournament stats, and head-to-head requests
4. Monitor response times and error rates

**Success Criteria**:
- ✅ 95th percentile response time < 500ms
- ✅ Chart rendering time < 2s
- ✅ Error rate < 1%
- ✅ Database connection pool stable

### Chart Performance
**Target**: Smooth rendering with large datasets

**Test Procedure**:
1. Load time series with 365+ data points (yearly view)
2. Test zoom/pan performance
3. Monitor browser memory usage
4. Test on various devices/browsers

**Success Criteria**:
- ✅ Initial chart load < 2s
- ✅ Zoom/pan operations < 100ms response
- ✅ Memory usage stable (no leaks)
- ✅ Works on mobile devices

## Database Performance

### Query Optimization
**Target**: Efficient statistical aggregation queries

**Test Queries**:
```sql
-- Test player time series aggregation
EXPLAIN ANALYZE
SELECT DATE_TRUNC('week', game_date) as week,
       AVG(score) as avg_score,
       PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY score) as median_score
FROM player_statistics
WHERE player_id = 1
  AND game_date >= '2025-01-01'
GROUP BY DATE_TRUNC('week', game_date)
ORDER BY week;

-- Test head-to-head query performance
EXPLAIN ANALYZE
SELECT * FROM head_to_head_record
WHERE (player1_id = 1 AND player2_id = 2)
   OR (player1_id = 2 AND player2_id = 1)
ORDER BY match_date DESC;
```

**Success Criteria**:
- ✅ Time series queries < 100ms
- ✅ Head-to-head queries < 50ms
- ✅ Aggregation queries use indexes effectively
- ✅ No full table scans on large tables

## UI Consistency Validation

### Design System Compliance
**Objective**: Verify visual consistency with existing UI

**Steps**:
1. Compare chart styling with existing components
2. Verify color scheme matches application theme
3. Test responsive behavior across screen sizes
4. Verify accessibility compliance (WCAG 2.1)

**Expected Results**:
- ✅ Charts use application color palette
- ✅ Typography matches design system
- ✅ Responsive breakpoints work correctly
- ✅ Accessibility features present (alt text, keyboard navigation)

## Integration Testing

### API Contract Validation
**Objective**: Verify all API endpoints work as specified

**Test Suite**:
```bash
# Run API contract tests
dotnet test StatisticsApi.ContractTests

# Expected tests:
# - PlayerTimeSeriesControllerTests
# - TournamentStatsControllerTests
# - HeadToHeadControllerTests
# - ChartDataControllerTests
```

**Success Criteria**:
- ✅ All contract tests pass
- ✅ Response schemas match OpenAPI specification
- ✅ Error responses follow consistent format
- ✅ Authentication/authorization works correctly

### End-to-End Testing
**Objective**: Verify complete user workflows

**Automated Tests** (using Playwright):
```csharp
[Test]
public async Task PlayerStatsWorkflow_ShouldDisplayCompleteStatistics()
{
    // Navigate to player stats
    await Page.GotoAsync("/players/1/stats");

    // Verify chart loads
    await Expect(Page.Locator(".chart-container canvas")).ToBeVisibleAsync();

    // Test metric toggle
    await Page.ClickAsync("[data-testid='median-toggle']");
    await Expect(Page.Locator(".chart-container")).ToContainTextAsync("Median");

    // Test period selector
    await Page.SelectOptionAsync("[data-testid='period-selector']", "monthly");
    await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    // Verify chart updated
    await Expect(Page.Locator(".chart-container")).ToBeVisibleAsync();
}
```

## Deployment Checklist

### Pre-Production Validation
- [ ] All test scenarios pass
- [ ] Performance benchmarks met
- [ ] Database migrations applied
- [ ] Chart.js assets properly bundled
- [ ] HTTPS configuration verified
- [ ] CDN configuration (if applicable)

### Production Readiness
- [ ] Monitoring alerts configured
- [ ] Error logging operational
- [ ] Backup procedures tested
- [ ] Rollback plan documented
- [ ] User training materials prepared

## Success Metrics

### Functional Success
- ✅ All 10 functional requirements (FR-001 through FR-010) implemented
- ✅ All acceptance scenarios pass testing
- ✅ Edge cases handled gracefully
- ✅ Performance targets achieved

### User Experience Success
- ✅ Charts load within 2 seconds
- ✅ Interactive features respond smoothly
- ✅ Mobile experience optimized
- ✅ Accessibility standards met

### Technical Success
- ✅ No breaking changes to existing functionality
- ✅ Database performance optimized
- ✅ Code coverage >80% for new components
- ✅ All extra metrics preserved and displayed

---

**Completion Criteria**: All test scenarios must pass before the feature is considered ready for production deployment.