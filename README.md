# Mahjong Stats

A simple web application to fetch and display Riichi Mahjong game records from the Mahjong Tracker App.

This was built only because original Mahjong Tracker doesn't have a statistics feature and I was too lazy to put the data in Excel and calculate stuff myself.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for local PostgreSQL)
- Google OAuth credentials (for authentication)

## Local Development Setup

### 1. Clone the repository
```bash
git clone https://github.com/KosmaKo/MahjongStats.git
cd MahjongStats
```

### 2. Install dependencies
```bash
dotnet restore
```

### 3. Set up PostgreSQL with Docker

Start a local PostgreSQL database using Docker:

```bash
docker run -d \
  --name mahjong-postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=mahjongstats \
  -p 5432:5432 \
  postgres:16
```

Or on Windows PowerShell:

```powershell
docker run -d --name mahjong-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=mahjongstats -p 5432:5432 postgres:16
```

**Note**: The database data persists in a Docker volume. To remove everything and start fresh:

```bash
docker stop mahjong-postgres
docker rm mahjong-postgres
docker volume prune
```

### 4. Configure environment variables

Create a `.env` file in the project root with your credentials:

```env
# Database Configuration (for local development)
DATABASE_URL=postgresql://postgres:postgres@localhost:5432/mahjongstats

# Google OAuth Configuration
Google__ClientId=your-google-client-id.apps.googleusercontent.com
Google__ClientSecret=your-google-client-secret

# Email whitelist for Refresh page access (add more as needed)
Auth__AllowedEmails__0=your-email@gmail.com

# MahjongTracker API Configuration
MahjongTracker__ApiUrl=YOUR_API_URL

# Environment
ASPNETCORE_ENVIRONMENT=Development
```

### 5. Set up Google OAuth

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the Google+ API (or Identity Toolkit API)
4. Go to **Credentials** → **Create Credentials** → **OAuth 2.0 Client ID**
5. Configure the OAuth consent screen if needed
6. Create **Web application** credentials
7. Add authorized redirect URIs:
   - `http://localhost:5188/signin-google`
   - `https://localhost:7218/signin-google`
8. Copy the Client ID and Client Secret to your `.env` file

### 6. Apply database migrations

The application automatically applies migrations on startup, but you can also run them manually:

```bash
dotnet ef database update
```

### 7. Run the application

```bash
dotnet run
```

The app will be available at:
- HTTP: http://localhost:5188
- HTTPS: https://localhost:7218

The database migrations will be applied automatically on first run.

## Production Deployment

### Deployment to Railway (Recommended)

Railway provides easy PostgreSQL hosting with automatic deployments from GitHub.

#### 1. Prepare your repository

Make sure your code is pushed to GitHub and `.env` is in `.gitignore`.

#### 2. Create a Railway project

1. Go to [Railway.app](https://railway.app) and sign in with GitHub
2. Click **New Project** → **Deploy from GitHub repo**
3. Select your `MahjongStats` repository
4. Railway will detect it as a .NET application

#### 3. Add PostgreSQL database

1. In your Railway project, click **New** → **Database** → **Add PostgreSQL**
2. Railway will create a PostgreSQL instance and automatically set the `DATABASE_URL` variable

#### 4. Configure environment variables

In Railway project settings, add these variables:

```env
# Google OAuth (from Google Cloud Console)
Google__ClientId=your-google-client-id.apps.googleusercontent.com
Google__ClientSecret=your-google-client-secret

# Authorized emails (add more as needed)
Auth__AllowedEmails__0=your-email@gmail.com

# MahjongTracker API
MahjongTracker__ApiUrl=YOUR_API_URL

# Environment
ASPNETCORE_ENVIRONMENT=Production
```

**Note**: `DATABASE_URL` is automatically set by Railway when you add PostgreSQL.

#### 5. Update Google OAuth redirect URIs

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Navigate to your OAuth credentials
3. Add your Railway domain to authorized redirect URIs:
   - `https://your-app.up.railway.app/signin-google`
   - Or your custom domain if configured

#### 6. Deploy

Railway will automatically:
- Build your application
- Apply database migrations on startup
- Deploy to a public URL
- Redeploy on every git push to main

### Deployment to Other Platforms (Heroku, Azure, etc.)

#### Required Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `DATABASE_URL` | PostgreSQL connection string (format: `postgresql://user:pass@host:port/dbname`) | Yes |
| `Google__ClientId` | Google OAuth Client ID | Yes |
| `Google__ClientSecret` | Google OAuth Client Secret | Yes |
| `Auth__AllowedEmails__0` | First authorized email | Yes |
| `Auth__AllowedEmails__1+` | Additional authorized emails (add more as needed) | No |
| `MahjongTracker__ApiUrl` | Mahjong Tracker API endpoint | Yes |
| `ASPNETCORE_ENVIRONMENT` | Environment (Production/Development) | Yes |

#### Database Setup

The application automatically:
- Parses PostgreSQL connection strings (both URI and standard formats)
- Applies all migrations on startup
- Creates tables with proper schema

**Connection String Formats Supported:**
- URI format: `postgresql://username:password@host:port/database`
- Standard format: `Host=host;Port=port;Database=dbname;Username=user;Password=pass`

**Important**: The application is configured for PostgreSQL only. SQLite is no longer supported.

### Using Docker

#### Build and run locally with Docker:

```bash
docker build -t mahjongstats .
docker run -p 8080:8080 --env-file .env mahjongstats
```

#### Docker Compose (with PostgreSQL):

```bash
docker-compose up
```

This will start both the application and PostgreSQL database.

## Features

- 🎮 Game history tracking and statistics from Mahjong Tracker
- 📊 Comprehensive player performance analytics
- 🎯 Advanced filtering (date range, specific players)
- 📈 Win rates, placement distribution, and score trends
- 🔐 Google OAuth authentication
- 🗄️ PostgreSQL database with automatic migrations
- 🎨 Responsive web interface
- 📌 Pin and compare player statistics
- 🔄 Real-time game data refresh

## Technology Stack

- **Backend**: ASP.NET Core 8.0
- **Frontend**: Blazor Server (Interactive Server-side rendering)
- **Database**: PostgreSQL 16 with Entity Framework Core
- **Authentication**: Google OAuth 2.0
- **Hosting**: Docker-ready, optimized for Railway deployment

## Troubleshooting

### Local Development Issues

**PostgreSQL connection errors:**
- Ensure Docker container is running: `docker ps | findstr mahjong-postgres`
- Restart container: `docker restart mahjong-postgres`
- Check connection string in `.env` file matches container settings

**Migration errors:**
- Drop and recreate database: `docker exec mahjong-postgres psql -U postgres -c "DROP DATABASE mahjongstats;" -c "CREATE DATABASE mahjongstats;"`
- Or remove container and start fresh (see step 3 above)

**OAuth errors:**
- Verify redirect URIs in Google Cloud Console match your application URLs
- Check that Google OAuth credentials are correctly set in `.env`

### Production Deployment Issues

**Database not updating:**
- Check Railway logs for migration errors
- Ensure `DATABASE_URL` environment variable is set correctly
- Verify PostgreSQL service is running in Railway dashboard

**Authentication not working:**
- Update Google OAuth redirect URIs with production domain
- Verify `Auth__AllowedEmails__0` is set with correct email address
- Check `Google__ClientId` and `Google__ClientSecret` are set

## Development Notes

### DateTime Handling

The application uses PostgreSQL's `timestamp with time zone` type and automatically converts all DateTime values to UTC using EF Core's `HasConversion`. This ensures consistent timezone handling across different environments.

### Database Migrations

To create a new migration:

```bash
dotnet ef migrations add MigrationName
```

Migrations are automatically applied on application startup via `dbContext.Database.Migrate()`.

## Security Notes

- ✅ Never commit `.env` or `appsettings.Development.json` to source control
- ✅ The `.gitignore` file is configured to exclude sensitive files
- ✅ Only emails in the `Auth__AllowedEmails` list can access the Refresh page
- ✅ Use HTTPS in production (automatically configured)
- ✅ Production uses SSL for PostgreSQL connections
- ✅ OAuth tokens are securely managed by ASP.NET Core Identity

## License

This project is for personal use. Feel free to fork and modify for your own needs.