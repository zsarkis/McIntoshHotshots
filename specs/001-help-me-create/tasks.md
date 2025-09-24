# Tasks: Advanced Stat Viewing System

**Input**: Design documents from `/specs/001-help-me-create/`
**Prerequisites**: plan.md (✓), research.md (✓), data-model.md (✓), contracts/ (✓), quickstart.md (✓)

## Execution Flow (main)
```
1. Load plan.md from feature directory
   → ✓ Loaded: Blazor Server with C#, PostgreSQL, Chart.js integration
   → ✓ Extract: Web application structure (integrated frontend/backend)
2. Load optional design documents:
   → ✓ data-model.md: 5 core entities → model tasks
   → ✓ contracts/: stats-api.yaml → 5 endpoints → contract test tasks
   → ✓ research.md: Chart.js, PostgreSQL aggregation → setup tasks
   → ✓ quickstart.md: 5 scenarios → integration test tasks
3. Generate tasks by category:
   → ✓ Setup: dependencies, Chart.js, database
   → ✓ Tests: 5 contract tests, 5 integration tests
   → ✓ Core: 5 models, 3 services, 5 controllers, 6 components
   → ✓ Integration: EF migrations, Chart.js interop
   → ✓ Polish: unit tests, performance optimization
4. Apply task rules:
   → ✓ Different files = marked [P] for parallel
   → ✓ Same file = sequential (no [P])
   → ✓ Tests before implementation (TDD)
5. Number tasks sequentially (T001, T002...)
6. Generate dependency graph
7. Create parallel execution examples
8. Validate task completeness: ✓ All requirements covered
```

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions
- **Blazor Server Web App**: Components in `Pages/`, Services in `Services/`, Models in `Models/`
- **Database**: Entity Framework migrations in `Data/Migrations/`
- **Frontend**: JavaScript interop in `wwwroot/js/`, CSS in `wwwroot/css/`
- **Tests**: xUnit tests in separate test project structure

## Phase 3.1: Setup & Dependencies

- [ ] **T001** [P] Add Chart.js NuGet package and configure Blazor JavaScript interop in project file
- [ ] **T002** [P] Install Entity Framework Core PostgreSQL provider and configure connection string
- [ ] **T003** [P] Create Chart.js JavaScript interop file at `wwwroot/js/chart-interop.js`
- [ ] **T004** [P] Configure CSS imports for Chart.js styling in `wwwroot/css/charts.css`
- [ ] **T005** Create database context class `Data/StatisticsDbContext.cs` with entity configurations

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### Contract Tests [P]
- [ ] **T006** [P] Contract test GET `/api/stats/player/{playerId}/timeseries` in `Tests/Contract/PlayerTimeSeriesControllerTests.cs`
- [ ] **T007** [P] Contract test GET `/api/stats/tournament/{tournamentId}/timeseries` in `Tests/Contract/TournamentStatsControllerTests.cs`
- [ ] **T008** [P] Contract test GET `/api/stats/headtohead/{player1Id}/{player2Id}` in `Tests/Contract/HeadToHeadControllerTests.cs`
- [ ] **T009** [P] Contract test GET `/api/stats/player/{playerId}/metrics` in `Tests/Contract/PlayerMetricsControllerTests.cs`
- [ ] **T010** [P] Contract test POST `/api/stats/chart-data` in `Tests/Contract/ChartDataControllerTests.cs`

### Integration Tests [P]
- [ ] **T011** [P] Integration test Player Time Series View scenario in `Tests/Integration/PlayerTimeSeriesIntegrationTests.cs`
- [ ] **T012** [P] Integration test Tournament Statistics View scenario in `Tests/Integration/TournamentStatsIntegrationTests.cs`
- [ ] **T013** [P] Integration test Head-to-Head Comparison scenario in `Tests/Integration/HeadToHeadIntegrationTests.cs`
- [ ] **T014** [P] Integration test Interactive Chart Features scenario in `Tests/Integration/ChartInteractionTests.cs`
- [ ] **T015** [P] Integration test Data Validation and Edge Cases scenario in `Tests/Integration/DataValidationTests.cs`

## Phase 3.3: Database Models & Migrations (ONLY after tests are failing)

### Entity Models [P]
- [ ] **T016** [P] Create PlayerStatistics entity model in `Models/PlayerStatistics.cs`
- [ ] **T017** [P] Create TournamentStatistics entity model in `Models/TournamentStatistics.cs`
- [ ] **T018** [P] Create HeadToHeadRecord entity model in `Models/HeadToHeadRecord.cs`
- [ ] **T019** [P] Create TimeSeriesDataPoint entity model in `Models/TimeSeriesDataPoint.cs`
- [ ] **T020** [P] Create MetricCalculation entity model in `Models/MetricCalculation.cs`

### Database Configuration
- [ ] **T021** Configure Entity Framework relationships and indexes in `Data/StatisticsDbContext.cs`
- [ ] **T022** Generate and apply initial database migration for statistics entities
- [ ] **T023** Create database seed data for testing in `Data/StatisticsSeedData.cs`

## Phase 3.4: Core Services Implementation

### Service Layer [P]
- [ ] **T024** [P] Create StatisticsService for data aggregation in `Services/StatisticsService.cs`
- [ ] **T025** [P] Create TimeSeriesService for temporal data processing in `Services/TimeSeriesService.cs`
- [ ] **T026** [P] Create ChartDataService for Chart.js formatting in `Services/ChartDataService.cs`

### Repository Pattern (Optional)
- [ ] **T027** [P] Create IStatisticsRepository interface in `Repositories/IStatisticsRepository.cs`
- [ ] **T028** [P] Implement StatisticsRepository with EF Core in `Repositories/StatisticsRepository.cs`

## Phase 3.5: API Controllers

- [ ] **T029** Create PlayerTimeSeriesController for `/api/stats/player/{id}/timeseries` endpoint
- [ ] **T030** Create TournamentStatsController for `/api/stats/tournament/{id}/timeseries` endpoint
- [ ] **T031** Create HeadToHeadController for `/api/stats/headtohead/{id1}/{id2}` endpoint
- [ ] **T032** Create PlayerMetricsController for `/api/stats/player/{id}/metrics` endpoint
- [ ] **T033** Create ChartDataController for `/api/stats/chart-data` endpoint

## Phase 3.6: Blazor Components

### Chart Components [P]
- [ ] **T034** [P] Create TimeSeriesChart component in `Components/Charts/TimeSeriesChart.razor`
- [ ] **T035** [P] Create HeadToHeadChart component in `Components/Charts/HeadToHeadChart.razor`
- [ ] **T036** [P] Create MetricToggle component in `Components/Controls/MetricToggle.razor`
- [ ] **T037** [P] Create DateRangeSelector component in `Components/Controls/DateRangeSelector.razor`
- [ ] **T038** [P] Create PeriodSelector component in `Components/Controls/PeriodSelector.razor`
- [ ] **T039** [P] Create TournamentFilter component in `Components/Controls/TournamentFilter.razor`

### Page Components
- [ ] **T040** Create PlayerStatsPage in `Pages/Players/PlayerStats.razor`
- [ ] **T041** Create TournamentStatsPage in `Pages/Tournaments/TournamentStats.razor`
- [ ] **T042** Create HeadToHeadPage in `Pages/Statistics/HeadToHead.razor`

## Phase 3.7: JavaScript Integration

- [ ] **T043** Implement Chart.js interop functions in `wwwroot/js/chart-interop.js`
- [ ] **T044** Create chart configuration and options in `wwwroot/js/chart-config.js`
- [ ] **T045** Add error handling and performance monitoring to chart interactions

## Phase 3.8: Integration & Middleware

- [ ] **T046** Configure dependency injection for all services in `Program.cs`
- [ ] **T047** Add error handling middleware for API endpoints
- [ ] **T048** Implement request/response logging for statistical operations
- [ ] **T049** Add CORS configuration for chart data endpoints
- [ ] **T050** Configure authentication/authorization for statistical data

## Phase 3.9: Performance Optimization

- [ ] **T051** Implement database query optimization with proper indexes
- [ ] **T052** Add caching layer for frequently accessed statistical calculations
- [ ] **T053** Optimize Chart.js rendering for large datasets (virtualization)
- [ ] **T054** Add database connection pooling configuration

## Phase 3.10: Polish & Unit Tests

### Unit Tests [P]
- [ ] **T055** [P] Unit tests for StatisticsService calculations in `Tests/Unit/StatisticsServiceTests.cs`
- [ ] **T056** [P] Unit tests for TimeSeriesService aggregation in `Tests/Unit/TimeSeriesServiceTests.cs`
- [ ] **T057** [P] Unit tests for ChartDataService formatting in `Tests/Unit/ChartDataServiceTests.cs`
- [ ] **T058** [P] Unit tests for entity validation rules in `Tests/Unit/EntityValidationTests.cs`

### Documentation & Validation
- [ ] **T059** [P] Update API documentation with statistical endpoints
- [ ] **T060** Create performance benchmarking tests (target: <500ms chart rendering)
- [ ] **T061** Execute complete quickstart.md validation scenarios
- [ ] **T062** Code review and refactoring for maintainability

## Dependencies

### Critical Dependencies
- **Setup** (T001-T005) before **All Implementation**
- **Tests** (T006-T015) before **Implementation** (T016-T054)
- **Models** (T016-T020) before **Services** (T024-T028)
- **Services** (T024-T028) before **Controllers** (T029-T033)
- **Controllers** (T029-T033) before **Components** (T034-T042)

### Specific Blocks
- T021 (DB Context) blocks T022 (Migration)
- T022 (Migration) blocks T023 (Seed Data)
- T027 (Repository Interface) blocks T028 (Repository Implementation)
- T043 (Chart Interop) blocks T034-T035 (Chart Components)
- T046 (DI Configuration) blocks T040-T042 (Page Components)

## Parallel Execution Examples

### Phase 3.2: Launch All Contract Tests Together
```bash
# Run these 5 tasks in parallel:
Task: "Contract test GET /api/stats/player/{playerId}/timeseries in Tests/Contract/PlayerTimeSeriesControllerTests.cs"
Task: "Contract test GET /api/stats/tournament/{tournamentId}/timeseries in Tests/Contract/TournamentStatsControllerTests.cs"
Task: "Contract test GET /api/stats/headtohead/{player1Id}/{player2Id} in Tests/Contract/HeadToHeadControllerTests.cs"
Task: "Contract test GET /api/stats/player/{playerId}/metrics in Tests/Contract/PlayerMetricsControllerTests.cs"
Task: "Contract test POST /api/stats/chart-data in Tests/Contract/ChartDataControllerTests.cs"
```

### Phase 3.3: Launch All Entity Models Together
```bash
# Run these 5 tasks in parallel:
Task: "Create PlayerStatistics entity model in Models/PlayerStatistics.cs"
Task: "Create TournamentStatistics entity model in Models/TournamentStatistics.cs"
Task: "Create HeadToHeadRecord entity model in Models/HeadToHeadRecord.cs"
Task: "Create TimeSeriesDataPoint entity model in Models/TimeSeriesDataPoint.cs"
Task: "Create MetricCalculation entity model in Models/MetricCalculation.cs"
```

### Phase 3.6: Launch All Chart Components Together
```bash
# Run these 6 tasks in parallel:
Task: "Create TimeSeriesChart component in Components/Charts/TimeSeriesChart.razor"
Task: "Create HeadToHeadChart component in Components/Charts/HeadToHeadChart.razor"
Task: "Create MetricToggle component in Components/Controls/MetricToggle.razor"
Task: "Create DateRangeSelector component in Components/Controls/DateRangeSelector.razor"
Task: "Create PeriodSelector component in Components/Controls/PeriodSelector.razor"
Task: "Create TournamentFilter component in Components/Controls/TournamentFilter.razor"
```

## Validation Checklist
*GATE: Checked before task execution*

- [✓] All 5 API contracts have corresponding tests (T006-T010)
- [✓] All 5 entities have model tasks (T016-T020)
- [✓] All tests come before implementation (Phase 3.2 before 3.3+)
- [✓] Parallel tasks truly independent (different files/components)
- [✓] Each task specifies exact file path
- [✓] No task modifies same file as another [P] task
- [✓] All functional requirements covered (FR-001 through FR-010)

## Completion Criteria

### Technical Success
- All 62 tasks completed successfully
- All tests passing (contract, integration, unit)
- Performance targets met (<500ms chart rendering, 1000+ concurrent users)
- Database migrations applied successfully

### Functional Success
- All 10 functional requirements implemented (FR-001 through FR-010)
- All 5 quickstart scenarios validated
- Chart.js integration working smoothly
- All existing "extra metrics" preserved and displayed

### Quality Assurance
- Code coverage >80% for new statistical components
- No breaking changes to existing functionality
- UI consistency maintained with existing design system
- Accessibility standards met (WCAG 2.1)

---

**Total Tasks**: 62 tasks across 10 phases
**Estimated Parallel Opportunities**: 28 tasks can run in parallel
**Critical Path**: Setup → Tests → Models → Services → Controllers → Components → Integration
**Ready for Execution**: ✓ All prerequisites satisfied