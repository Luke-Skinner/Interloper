using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Interloper.Discord.Services;

// Hosted service that configures Hangfire recurring jobs on startup
public class HangfireJobScheduler : IHostedService
{
    private readonly IRecurringJobManager _recurringJobs;
    private readonly ILogger<HangfireJobScheduler> _logger;

    public HangfireJobScheduler(
        IRecurringJobManager recurringJobs,
        ILogger<HangfireJobScheduler> logger)
    {
        _recurringJobs = recurringJobs;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuring Hangfire recurring jobs");

        // Process due alerts every 5 minutes
        _recurringJobs.AddOrUpdate<AlertCheckService>(
            "process-due-alerts",
            "alerts",
            service => service.ProcessDueAlertsAsync(),
            "*/5 * * * *", // Every 5 minutes
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        _logger.LogInformation("Hangfire jobs configured successfully");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hangfire job scheduler stopping");
        return Task.CompletedTask;
    }
}
