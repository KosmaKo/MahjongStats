# Mahjong Stats

A simple web application to fetch and display Riichi Mahjong game records from the Mahjong Tracker App.

This was built only because original Mahjong Tracker doesn't have a statistics feature and I was too lazy to put the data in Excel and calculate stuff myself.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Google OAuth credentials (for authentication)

## Local Development Setup

### 1. Clone the repository
```bash
git clone <repository-url>
cd MahjongStats
```

### 2. Install dependencies
```bash
dotnet restore
```

### 3. Configure environment variables

Copy `.env.example` to `.env` and fill in your credentials:

```bash
cp .env.example .env
```

Edit `.env` with your values:

```env
# Google OAuth Configuration
Google__ClientId=your-google-client-id.apps.googleusercontent.com
Google__ClientSecret=your-google-client-secret

# Email whitelist for Refresh page access
Auth__AllowedEmails__0=your-email@gmail.com

# MahjongTracker API Configuration
MahjongTracker__ApiUrl=YOUR_API_URL
MahjongTracker__BearerToken=YOUR_BEARER_TOKEN

# Environment
ASPNETCORE_ENVIRONMENT=Development
```

### 4. Set up Google OAuth

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the Google+ API
4. Go to **Credentials** → **Create Credentials** → **OAuth 2.0 Client ID**
5. Configure the OAuth consent screen if needed
6. Create **Web application** credentials
7. Add authorized redirect URIs:
   - `http://localhost:5188/signin-google`
   - `https://localhost:7218/signin-google`
8. Copy the Client ID and Client Secret to your `.env` file

### 5. Run the application

```bash
dotnet run
```

The app will be available at:
- HTTP: http://localhost:5188
- HTTPS: https://localhost:7218

The SQLite database will be created automatically on first run.

## Production Deployment

### Environment Variables Required

| Variable | Description | Required |
|----------|-------------|----------|
| `Google__ClientId` | Google OAuth Client ID | Yes |
| `Google__ClientSecret` | Google OAuth Client Secret | Yes |
| `Auth__AllowedEmails__0` | First authorized email | Yes |
| `Auth__AllowedEmails__1` | Additional authorized emails (add more as needed) | No |
| `MahjongTracker__ApiUrl` | Mahjong Tracker API endpoint | Yes |
| `MahjongTracker__BearerToken` | Bearer token for API authentication | Yes |
| `ASPNETCORE_ENVIRONMENT` | Environment (Production/Development) | Yes |
| `ConnectionStrings__DefaultConnection` | Database connection string (defaults to SQLite) | No |

### Database Setup in Production

**No manual database setup is required!** The application automatically:
- Creates the database on first run
- Applies all migrations automatically
- Uses SQLite by default with file: `MahjongStats.db`

**⚠️ Data Persistence:**
- **Railway/Cloud platforms**: By default, filesystem storage is **ephemeral** - data will be lost on redeployment
- **Solutions for persistent data**:
  1. **Add a persistent volume** (Railway: add a volume and mount to `/app/data`, then set `ConnectionStrings__DefaultConnection=Data Source=/app/data/MahjongStats.db`)
  2. **Use PostgreSQL/MySQL** (set `ConnectionStrings__DefaultConnection` to your database URL)

### Deployment to Railway

1. **Push your code to GitHub** (make sure `.env` is in `.gitignore`)

2. **Create a new project (with persistent storage):

```bash
docker build -t mahjongstats .
docker run -p 5188:8080 -v $(pwd)/data:/app/data --env-file .env mahjongstats
```

Or use Docker Compose:

```bash
docker-compose up
```

**Note**: The volume mount `-v $(pwd)/data:/app/data` ensures data persists between container restarts.**Update Google OAuth redirect URIs**:
   - Add your Railway domain: `https://your-app.up.railway.app/signin-google`

7. **Deploy** - Railway will automatically build and deploy

### Using Docker

Build and run with Docker:

```bash
docker build -t mahjongstats .
docker run -p 5188:8080 --env-file .env mahjongstats
```

Or use Docker Compose:

```bash
docker-compose up
```

## Features

- Google OAuth authentication
- Game history tracking and statistics
- Player performance analytics
- SQLite database with automatic migrations
- Responsive web interface

## Technology Stack

- ASP.NET Core 8.0
- Blazor Server
- Entity Framework Core
- SQLite
- Google OAuth 2.0

## Security Notes

- Never commit `.env` or `appsettings.Development.json` to source control
- The `.gitignore` file is configured to exclude sensitive files
- Only emails in the `Auth__AllowedEmails` list can access the Refresh page
- Use HTTPS in production (automatically configured)