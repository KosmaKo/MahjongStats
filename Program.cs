using Microsoft.EntityFrameworkCore;
using MahjongStats.Components;
using MahjongStats.Data;
using MahjongStats.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Claims;
using Npgsql;

namespace MahjongStats
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            
            // Add database context - supports both SQLite and PostgreSQL
            // Priority: Environment variable > appsettings config > default SQLite
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? builder.Configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=/app/data/MahjongStats.db";
            var isDevelopment = builder.Environment.IsDevelopment();
            
            // Log connection string details for debugging (first 50 chars to hide credentials)
            var displayString = string.IsNullOrEmpty(connectionString) 
                ? "[EMPTY]" 
                : connectionString.Length > 50 
                    ? connectionString.Substring(0, 50) + "..." 
                    : connectionString;
            Console.WriteLine($"[Database] Using connection string: {displayString}");
            Console.WriteLine($"[Database] Is Development: {isDevelopment}");
            
            builder.Services.AddDbContext<MahjongStatsContext>(options =>
            {
                // Use PostgreSQL if connection string contains "postgresql" or "postgres"
                if (!string.IsNullOrEmpty(connectionString) && (connectionString.Contains("postgresql", StringComparison.OrdinalIgnoreCase) 
                    || connectionString.Contains("postgres", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("[Database] Detected PostgreSQL connection string");
                    try
                    {
                        // If it's a URI (starts with postgresql://), convert it to Npgsql connection string format
                        if (connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("[Database] Converting PostgreSQL URI to connection string format");
                            var uri = new Uri(connectionString);
                            var connStrBuilder = new NpgsqlConnectionStringBuilder
                            {
                                Host = uri.Host,
                                Port = uri.Port > 0 ? uri.Port : 5432,
                                Database = uri.AbsolutePath.TrimStart('/'),
                                Username = uri.UserInfo?.Split(':')[0],
                                Password = uri.UserInfo?.Split(':').Length > 1 ? uri.UserInfo.Split(':')[1] : null,
                                SslMode = SslMode.Require
                            };
                            var convertedConnectionString = connStrBuilder.ConnectionString;
                            Console.WriteLine($"[Database] Converted connection string: {convertedConnectionString.Substring(0, Math.Min(50, convertedConnectionString.Length))}...");
                            options.UseNpgsql(convertedConnectionString);
                        }
                        else
                        {
                            // Already in connection string format
                            Console.WriteLine("[Database] Using connection string as-is");
                            options.UseNpgsql(connectionString);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Database] Error parsing PostgreSQL connection: {ex.Message}");
                        throw;
                    }
                }
                else if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Data Source", StringComparison.OrdinalIgnoreCase))
                {
                    // Default to SQLite only for valid SQLite connection strings
                    Console.WriteLine("[Database] Detected SQLite connection string");
                    options.UseSqlite(connectionString);
                }
                else
                {
                    // Fallback to SQLite if connection string format is unclear
                    Console.WriteLine("[Database] Defaulting to SQLite fallback");
                    options.UseSqlite("Data Source=/app/data/MahjongStats.db");
                }
            });

            builder.Services.AddHttpClient<IMahjongTrackerService, MahjongTrackerService>();
            builder.Services.AddScoped<IGameFilterService, GameFilterService>();
            builder.Services.AddScoped<IPlayerStatsService, PlayerStatsService>();
            builder.Services.AddScoped<IDatabaseService, DatabaseService>();
            builder.Services.AddScoped<IAuthService, AuthService>();

            // Add Cascading Authentication State for Blazor components
            builder.Services.AddCascadingAuthenticationState();
            
            // Add Authentication services
            builder.Services.AddAuthentication(o =>
            {
                o.DefaultScheme = "Cookies";
                o.DefaultChallengeScheme = "Google";
            })
            .AddCookie("Cookies")
            .AddGoogle(o =>
            {
                o.ClientId = builder.Configuration["Google:ClientId"] ?? "";
                o.ClientSecret = builder.Configuration["Google:ClientSecret"] ?? "";
                o.CallbackPath = "/signin-google";
                o.Scope.Clear();
                o.Scope.Add("openid");
                o.Scope.Add("profile");
                o.Scope.Add("email");
                o.ClaimActions.MapJsonKey("urn:google:picture", "picture");
                o.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
            });
            
            builder.Services.AddAuthorization();

            var app = builder.Build();
            
            // Configure forwarded headers for Railway (MUST be before authentication)
            var forwardedHeadersOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
            };
            forwardedHeadersOptions.KnownNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();
            app.UseForwardedHeaders(forwardedHeadersOptions);
            
            // Initialize database
            try
            {
                using (var scope = app.Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<MahjongStatsContext>();
                    Console.WriteLine("[Database] Running database migrations...");
                    dbContext.Database.Migrate();
                    Console.WriteLine("[Database] Database migration completed successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Database] Error during database initialization: {ex.GetType().Name}");
                Console.WriteLine($"[Database] Error: {ex.Message}");
                // Don't crash the app if there's a migration error - log it and continue
                Console.WriteLine($"[Database] Continuing despite migration error...");
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();
            
            // Map authentication endpoints
            app.MapGet("/auth/login", (HttpContext context, string? returnUrl = null) =>
            {
                var redirectUri = !string.IsNullOrEmpty(returnUrl) ? returnUrl : "/";
                return Results.Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, new[] { "Google" });
            });
            
            app.MapGet("/auth/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync("Cookies");
                return Results.Redirect("/");
            });
            
            app.MapGet("/auth/check", (HttpContext context) =>
            {
                var user = context.User;
                return Results.Json(new
                {
                    authenticated = user?.Identity?.IsAuthenticated ?? false,
                    email = user?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value 
                        ?? user?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                });
            });

            app.Run();
        }
    }
}
