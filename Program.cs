using Microsoft.EntityFrameworkCore;
using MahjongStats.Components;
using MahjongStats.Data;
using MahjongStats.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

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
            
            // Add database context
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=MahjongStats.db";
            builder.Services.AddDbContext<MahjongStatsContext>(options =>
                options.UseSqlite(connectionString));

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
                o.Scope.Clear();
                o.Scope.Add("openid");
                o.Scope.Add("profile");
                o.Scope.Add("email");
                o.ClaimActions.MapJsonKey("urn:google:picture", "picture");
                o.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
            });
            
            builder.Services.AddAuthorization();

            var app = builder.Build();
            
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
