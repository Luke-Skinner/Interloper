using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DotNetEnv;
using Hangfire;
using Hangfire.PostgreSql;
using Interloper.Core.Interfaces;
using Interloper.Data;
using Interloper.Data.Repositories;
using Interloper.Discord.Commands;
using Interloper.Discord.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Interloper.Discord;

class Program
{
    public static async Task Main(string[] args)
    {
        // Load .env file from project root
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }
        else if (File.Exists(".env"))
        {
            Env.Load();
        }

        // Set up Serilog configuration
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("logs/bot-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Starting Interloper Discord Bot");

            var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                // Configure Discord client
                services.AddSingleton(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.Guilds,
                    AlwaysDownloadUsers = false,
                    MessageCacheSize = 50
                });

                services.AddSingleton<DiscordSocketClient>();
                services.AddSingleton(provider =>
                    new InteractionService(provider.GetRequiredService<DiscordSocketClient>()));

                // Configure database
                var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                    ?? configuration.GetConnectionString("PostgreSQL");

                services.AddDbContext<InterloperDbContext>(options =>
                    options.UseNpgsql(connectionString));

                // Configure Hangfire
                services.AddHangfire(config => config
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UsePostgreSqlStorage(options =>
                        options.UseNpgsqlConnection(connectionString)));

                services.AddHangfireServer(options =>
                {
                    options.WorkerCount = 2;
                    options.Queues = new[] { "alerts", "default" };
                });

                // Register repositories
                services.AddScoped<IUserRepository, UserRepository>();
                services.AddScoped<IAlertRepository, AlertRepository>();

                // Register command modules
                services.AddScoped<AlertCommands>();

                // Configure scraper API client
                var scraperBaseUrl = Environment.GetEnvironmentVariable("SCRAPER_API_URL")
                    ?? configuration["Scraper:BaseUrl"]
                    ?? "http://localhost:8000";

                services.AddHttpClient<IScraperApiClient, ScraperApiClient>(client =>
                {
                    client.BaseAddress = new Uri(scraperBaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(60);
                });

                // Register services
                services.AddSingleton<NotificationService>();
                services.AddScoped<AlertCheckService>();
                services.AddHostedService<DiscordBotService>();
                services.AddHostedService<HangfireJobScheduler>();
                services.AddSingleton<InteractionHandler>();
            });
}
