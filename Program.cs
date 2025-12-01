using Microsoft.EntityFrameworkCore;
using MahjongStats.Components;
using MahjongStats.Data;
using MahjongStats.Services;

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
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
