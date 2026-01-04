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
            // Load .env file if it exists
            DotNetEnv.Env.Load();

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // PostgreSQL database configuration
            var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? builder.Configuration.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Database connection string not found. Set DATABASE_URL environment variable or DefaultConnection in appsettings.json");
            }

            Console.WriteLine($"[Database] Connection string detected: {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");

            builder.Services.AddDbContext<MahjongStatsContext>(options =>
            {
                // Parse PostgreSQL URI format (postgresql://user:pass@host:port/db)
                if (connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(connectionString);
                    var connBuilder = new NpgsqlConnectionStringBuilder
                    {
                        Host = uri.Host,
                        Port = uri.Port > 0 ? uri.Port : 5432,
                        Database = uri.AbsolutePath.TrimStart('/'),
                        Username = uri.UserInfo?.Split(':')[0],
                        Password = uri.UserInfo?.Split(':').Length > 1 ? uri.UserInfo.Split(':')[1] : null,
                        SslMode = SslMode.Require
                    };
                    Console.WriteLine("[Database] Using PostgreSQL with URI format");
                    options.UseNpgsql(connBuilder.ConnectionString);
                }
                else
                {
                    Console.WriteLine("[Database] Using PostgreSQL with connection string format");
                    options.UseNpgsql(connectionString);
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
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MahjongStatsContext>();
                dbContext.Database.Migrate();
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
