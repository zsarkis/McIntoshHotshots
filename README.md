# McIntosh Hotshots - Dart Tournament Management System

A comprehensive web application for organizing local dart tournament series and tracking player performance with advanced analytics and AI-powered coaching.

## 🎯 Overview

McIntosh Hotshots is a full-featured dart tournament management platform that combines tournament organization, live scoring, detailed performance analytics, and AI-powered coaching insights. Built with modern web technologies, it provides everything needed to run professional-quality dart tournaments while helping players improve their game.

## ✨ Key Features

### 🏆 Tournament Management
- **Tournament Creation**: Set up tournaments with configurable pool counts and attendee limits
- **Player Registration**: Manage player profiles and tournament registrations
- **Bracket Management**: Organize tournament brackets and match scheduling
- **Admin Controls**: Administrative dashboard for tournament oversight

### 🎮 Live Scoring System
- **Real-time Match Scoring**: Interactive dartboard interface for live match scoring
- **Advanced Rule Engine**: Automatic validation of dart rules and checkout possibilities
- **Match State Management**: Track current scores, legs won, and match progression
- **Live Updates**: Real-time score updates for spectators and participants

### 📊 Performance Analytics
- **Player Statistics**: Comprehensive performance metrics including:
  - 3-dart averages and consistency tracking
  - First 9 dart averages for match starts
  - Checkout percentages and highest finishes
  - Turn efficiency and scoring patterns
- **ELO Rating System**: Competitive ranking system for fair matchmaking
- **Detailed Match Analysis**: Leg-by-leg performance breakdowns
- **Comparative Analytics**: Head-to-head player performance comparisons
- **Historical Tracking**: Long-term performance trends and improvement tracking

### 🤖 AI-Powered Coaching
- **Performance Insights**: AI-generated coaching recommendations based on play data
- **Weakness Analysis**: Identification of common problem areas and improvement suggestions
- **Personalized Feedback**: Tailored advice based on individual playing style and statistics
- **Coaching Debug Tools**: Advanced analytics for performance optimization

### 🔗 DartConnect Integration
- **Automated Data Import**: Parse and import match data from DartConnect reports
- **Web Scraping**: Extract detailed match information including:
  - Match summaries and player statistics
  - Leg-by-leg throw details and scoring patterns
  - Time elapsed and match progression data
- **Data Validation**: Automatic player matching and data verification

### 📱 User Experience
- **Responsive Design**: Modern, mobile-friendly interface
- **Player Profiles**: Personalized dashboards with performance tracking
- **Notification System**: Match reminders and tournament updates
- **Authentication**: Secure user management with role-based access

## 🚀 Technology Stack

- **Backend**: ASP.NET Core 8.0 with Blazor Server
- **Database**: PostgreSQL with Entity Framework Core
- **Frontend**: Blazor Server Components with real-time SignalR updates
- **Data Access**: Repository pattern with Dapper for complex queries
- **Authentication**: ASP.NET Core Identity
- **Web Scraping**: PuppeteerSharp for DartConnect integration
- **Containerization**: Docker with multi-stage builds
- **Deployment**: Fly.io with automated CI/CD

## 🏗️ Architecture

The application follows a clean architecture pattern with:
- **Controllers**: API endpoints for external integrations
- **Services**: Business logic and application services
- **Repositories**: Data access layer with interface abstractions
- **Models**: Domain entities and data transfer objects
- **Components**: Blazor Server components for interactive UI

Key services include:
- `TournamentService`: Tournament management logic
- `UserPerformanceService`: Player analytics and statistics
- `CoachingService`: AI-powered coaching insights
- `LiveMatchService`: Real-time match state management
- `EloCalculationService`: Competitive ranking calculations
- `DartConnectReportParsingService`: External data integration

## 🛠️ Getting Started

### Prerequisites
- .NET 8.0 SDK
- PostgreSQL database
- Docker (optional)

### Local Development

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd McIntoshHotshots
   ```

2. **Navigate to the project directory**
   ```bash
   cd McIntoshHotshots
   ```

3. **Configure the database connection**
   Update `appsettings.Development.json` with your PostgreSQL connection string:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=mcintoshhotshots;Username=your_user;Password=your_password"
     }
   }
   ```

4. **Run database migrations**
   ```bash
   dotnet ef database update
   ```

5. **Start the application**
   ```bash
   dotnet run --urls="http://localhost:5000"
   ```

6. **Access the application**
   Open your browser and navigate to `http://localhost:5000`

### Docker Deployment

```bash
# Build the Docker image
docker build -t mcintoshhotshots .

# Run with Docker Compose
docker-compose up
```

## 🔧 Configuration

### Environment Variables
- `DATABASE_URL`: PostgreSQL connection string (production)
- `OpenAI__ApiKey`: OpenAI API key for coaching features (optional)

### Application Settings
Key configuration options in `appsettings.json`:
- Database connection strings
- Authentication settings
- Tournament default settings
- Performance analytics parameters

## 📊 Database Schema

The application uses PostgreSQL with the following key entities:
- **Players**: User profiles and preferences
- **Tournaments**: Tournament definitions and settings
- **MatchSummary**: Match results and statistics
- **Legs**: Individual leg data within matches
- **LegDetail**: Detailed throw-by-throw data
- **LiveMatch**: Real-time match state management

## 🎯 Usage

### For Tournament Organizers
1. Create tournaments with specific rules and settings
2. Manage player registrations and brackets
3. Monitor live matches and results
4. Generate tournament reports and analytics

### For Players
1. Register for tournaments and manage your profile
2. View detailed performance statistics and trends
3. Receive AI-powered coaching insights
4. Track improvement over time with historical data

### For Spectators
1. Follow live matches with real-time scoring
2. View player rankings and statistics
3. Access tournament brackets and results

## 🤝 Contributing

This project is designed for the McIntosh Hotshots dart community but can be adapted for other dart organizations. Contributions are welcome for:
- Additional analytics features
- UI/UX improvements
- Performance optimizations
- Integration with other dart scoring systems

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🚀 Deployment

### Fly.io Deployment

The application is configured for deployment to Fly.io with the app name `mcintoshhotshots-dev`.

#### Prerequisites
- Install the [Fly CLI](https://fly.io/docs/hands-on/install-flyctl/)
- Authenticate with Fly.io: `fly auth login`
- Ensure you have access to the `mcintoshhotshots-dev` app

#### Setting Up Secrets

To configure sensitive values like database connections and API keys, use Fly.io's secrets system. These values are injected into your app as environment variables and are **not stored in your codebase**.

Run the following commands from your terminal, replacing the values as needed:

```bash
# Database connection string for PostgreSQL
fly secrets set DATABASE_URL='Host=YOUR_HOST;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD' --app mcintoshhotshots-dev

# OpenAI API configuration for coaching features
fly secrets set OPENAI__APIKEY='your_openai_api_key' --app mcintoshhotshots-dev
fly secrets set OPENAI__ENDPOINT='https://api.openai.com/v1/responses' --app mcintoshhotshots-dev
fly secrets set OPENAI__MODEL='gpt-4o' --app mcintoshhotshots-dev
```

**Note**: The double underscores (`__`) in `OPENAI__APIKEY` map to colons (`:`) in the .NET configuration system. So `OPENAI__APIKEY` becomes accessible as `OpenAI:ApiKey` in your app.

#### Required Secret Values

| Secret | Description | Example |
|--------|-------------|---------|
| `DATABASE_URL` | PostgreSQL connection string for your Fly.io database | `Host=your-db.internal;Database=mcintoshhotshots;Username=postgres;Password=your_password` |
| `OPENAI__APIKEY` | Your OpenAI API key for coaching features | `sk-...` |
| `OPENAI__ENDPOINT` | OpenAI API endpoint URL | `https://api.openai.com/v1/responses` |
| `OPENAI__MODEL` | OpenAI model to use for coaching | `gpt-4o` |

#### Deploying the Application

After updating secrets, you must redeploy the app so the new values are picked up:

```bash
fly deploy --app mcintoshhotshots-dev
```

This rebuilds and restarts the app with the updated secrets.

#### Deployment Pipeline Features
- Automated Docker builds
- Database migrations
- Environment-specific configurations
- Health checks and monitoring

## 🔮 Future Enhancements

- Mobile app for iOS and Android
- Advanced tournament formats (round-robin, Swiss system)
- Video replay integration
- Enhanced AI coaching with computer vision
- Social features and community engagement
- Integration with additional dart scoring platforms

---

**McIntosh Hotshots** - Elevating your dart game with professional tournament management and intelligent performance analytics.

🚀 Auto-deployment to Fly.io dev environment is now configured!
