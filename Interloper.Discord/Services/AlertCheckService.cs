using Interloper.Core.Interfaces;
using Interloper.Core.Models;
using Microsoft.Extensions.Logging;

namespace Interloper.Discord.Services;

// Service that checks alerts for price matches and sends notifications
public class AlertCheckService
{
    private readonly IAlertRepository _alertRepository;
    private readonly IScraperApiClient _scraperClient;
    private readonly NotificationService _notificationService;
    private readonly ILogger<AlertCheckService> _logger;

    public AlertCheckService(
        IAlertRepository alertRepository,
        IScraperApiClient scraperClient,
        NotificationService notificationService,
        ILogger<AlertCheckService> logger)
    {
        _alertRepository = alertRepository;
        _scraperClient = scraperClient;
        _notificationService = notificationService;
        _logger = logger;
    }

    // Main job: Process all alerts that are due for checking
    public async Task ProcessDueAlertsAsync()
    {
        _logger.LogInformation("Starting alert check job");

        try
        {
            var dueAlerts = await _alertRepository.GetAlertsDueForCheckAsync();
            var alertList = dueAlerts.ToList();

            _logger.LogInformation("Found {Count} alerts due for checking", alertList.Count);

            foreach (var alert in alertList)
            {
                try
                {
                    await ProcessAlertAsync(alert);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing alert {AlertId}", alert.Id);
                }
            }

            _logger.LogInformation("Alert check job completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alert check job failed");
            throw;
        }
    }

    // Process a single alert
    private async Task ProcessAlertAsync(Alert alert)
    {
        _logger.LogDebug("Processing alert {AlertId} for user {UserId}", alert.Id, alert.UserId);

        var deals = await GetDealsFromScraperAsync(alert);

        if (deals.Count > 0)
        {
            _logger.LogInformation(
                "Found {Count} deals for alert {AlertId} in {City}",
                deals.Count, alert.Id, alert.City);

            var sent = await _notificationService.SendDealsSummaryAsync(
                alert.UserId,
                alert,
                deals);

            if (sent)
            {
                alert.TimesTriggered++;
            }
        }

        // Update alert timestamps
        alert.LastCheckedAt = DateTime.UtcNow;
        alert.NextCheckAt = CalculateNextCheckTime(alert.CheckFrequency);
        await _alertRepository.UpdateAsync(alert);

        _logger.LogDebug("Alert {AlertId} next check at {NextCheck}", alert.Id, alert.NextCheckAt);
    }

    // Call the Python scraper service
    private async Task<List<HotelDeal>> GetDealsFromScraperAsync(Alert alert)
    {
        var request = new ScraperRequest
        {
            City = alert.City,
            CheckIn = alert.CheckIn,
            CheckOut = alert.CheckOut,
            Guests = alert.Guests,
            HotelName = alert.HotelName,
            MaxPrice = alert.MaxPrice,
            MinRating = alert.MinRating > 0 ? alert.MinRating : null,
            FreeCancellation = alert.FreeCancellation,
        };

        ScraperResponse response;

        if (!string.IsNullOrEmpty(alert.HotelName))
        {
            response = await _scraperClient.ScrapeHotelAsync(request);
        }
        else
        {
            response = await _scraperClient.ScrapeCityAsync(request);
        }

        if (!response.Success)
        {
            _logger.LogWarning(
                "Scraper failed for alert {AlertId}: {Error}",
                alert.Id, response.ErrorMessage);
            return new List<HotelDeal>();
        }

        // Convert scraper results to HotelDeal objects
        // Only include hotels that are actually below the max price
        return response.Hotels
            .Where(h => h.Price <= alert.MaxPrice)
            .Select(h => new HotelDeal
            {
                HotelName = h.Name,
                Price = h.Price,
                Platform = h.Platform,
                Rating = h.Rating,
                BookingUrl = h.BookingUrl,
            })
            .ToList();
    }

    // Calculate next check time based on frequency
    private static DateTime CalculateNextCheckTime(string frequency)
    {
        var now = DateTime.UtcNow;

        return frequency.ToLower() switch
        {
            "hourly" => now.AddHours(1),
            "6h" => now.AddHours(6),
            "12h" => now.AddHours(12),
            "daily" => now.AddDays(1),
            "weekly" => now.AddDays(7),
            _ => now.AddDays(1)
        };
    }
}
