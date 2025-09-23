# Claude Code Context

## Project Overview
McIntosh Hotshots - Dart tournament management system with advanced statistical viewing capabilities.

## Current Feature: Advanced Stat Viewing System (001-help-me-create)

### Technology Stack
- **Backend**: C# 8.0+, ASP.NET Core, Entity Framework Core
- **Frontend**: Blazor Server, HTML5/CSS3, JavaScript (Chart.js)
- **Database**: PostgreSQL
- **Testing**: xUnit, Playwright
- **Charts**: Chart.js with Blazor JavaScript interop

### Architecture
- **Project Type**: Web application (Blazor Server)
- **Structure**: Integrated frontend/backend with Blazor components
- **Performance Goals**: <500ms chart rendering, 1000+ concurrent users
- **Data Scale**: ~50k player records, ~10k tournaments

### Key Components
- Time series statistical visualization
- Head-to-head player comparisons
- Interactive charts with zoom/pan/filter
- Real-time metric toggles (average/median)
- Tournament statistical analysis

### Recent Changes
- Added statistical data models (PlayerStatistics, TournamentStatistics, HeadToHeadRecord)
- Created API contracts for statistical endpoints
- Implemented Chart.js integration patterns
- Added performance optimization indexes

### Development Guidelines
- Follow test-first development (TDD)
- Maintain UI consistency with existing Bootstrap framework
- Preserve all current extra metrics (completion rates, checkout %, doubles hit rate)
- Use PostgreSQL window functions for time series aggregation
- Implement graceful degradation for insufficient data

### API Endpoints
- `GET /api/stats/player/{id}/timeseries` - Player time series data
- `GET /api/stats/tournament/{id}/timeseries` - Tournament statistics
- `GET /api/stats/headtohead/{id1}/{id2}` - Head-to-head comparison
- `POST /api/stats/chart-data` - Chart.js formatted data

### Performance Considerations
- Pre-aggregate time series data for faster chart rendering
- Use database-level statistical calculations
- Implement caching for complex calculations
- Optimize for 50k+ player records with efficient indexing