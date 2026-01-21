using Discord;
using Discord.Interactions;
using Interloper.Core.Interfaces;
using Interloper.Core.Models;
using Microsoft.Extensions.Logging;

namespace Interloper.Discord.Commands;

// Slash commands for managing price alerts
public class AlertCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<AlertCommands> _logger;
    private readonly IAlertRepository _alertRepository;
    private readonly IUserRepository _userRepository;

    public AlertCommands(
        ILogger<AlertCommands> logger,
        IAlertRepository alertRepository,
        IUserRepository userRepository)
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _userRepository = userRepository;
    }

    [SlashCommand("alert-create", "Create a new hotel price alert")]
    public async Task CreateAlertAsync(
        [Summary("city", "City to search (e.g., 'New York')")] string city,
        [Summary("checkin", "Check-in date (YYYY-MM-DD)")] string checkIn,
        [Summary("checkout", "Check-out date (YYYY-MM-DD)")] string checkOut,
        [Summary("max-price", "Maximum price per night in USD")] decimal maxPrice,
        [Summary("guests", "Number of guests")] int guests = 2,
        [Summary("hotel", "Specific hotel name (optional)")] string? hotelName = null,
        [Summary("frequency", "Check frequency: hourly, 6h, 12h, daily, weekly")] string frequency = "daily")
    {
        _logger.LogInformation("CreateAlertAsync called for city: {City}", city);

        try
        {
            // Validate dates
            if (!DateOnly.TryParse(checkIn, out var checkInDate))
            {
                await FollowupAsync("Invalid check-in date format. Use YYYY-MM-DD.", ephemeral: true);
                return;
            }

            if (!DateOnly.TryParse(checkOut, out var checkOutDate))
            {
                await FollowupAsync("Invalid check-out date format. Use YYYY-MM-DD.", ephemeral: true);
                return;
            }

            if (checkOutDate <= checkInDate)
            {
                await FollowupAsync("Check-out date must be after check-in date.", ephemeral: true);
                return;
            }

            if (checkInDate < DateOnly.FromDateTime(DateTime.UtcNow))
            {
                await FollowupAsync("Check-in date cannot be in the past.", ephemeral: true);
                return;
            }

            // Validate frequency
            var validFrequencies = new[] { "hourly", "6h", "12h", "daily", "weekly" };
            if (!validFrequencies.Contains(frequency.ToLower()))
            {
                await FollowupAsync($"Invalid frequency. Choose from: {string.Join(", ", validFrequencies)}", ephemeral: true);
                return;
            }

            // Get or create user
            var discordId = (long)Context.User.Id;
            var user = await _userRepository.GetOrCreateAsync(discordId, Context.User.Username);

            // Create the alert
            var alert = new Alert
            {
                UserId = discordId,
                AlertType = string.IsNullOrEmpty(hotelName) ? "city" : "hotel",
                HotelName = hotelName,
                City = city,
                CheckIn = checkInDate,
                CheckOut = checkOutDate,
                Guests = guests,
                MaxPrice = maxPrice,
                CheckFrequency = frequency.ToLower(),
                NextCheckAt = DateTime.UtcNow,
                IsActive = true
            };

            await _alertRepository.CreateAsync(alert);

            _logger.LogInformation("Alert {AlertId} created by user {UserId} for {City}",
                alert.Id, discordId, city);

            // Build response embed
            var embedBuilder = new EmbedBuilder()
                .WithTitle("Alert Created")
                .WithColor(Color.Green)
                .WithDescription("Your price alert has been created successfully.")
                .AddField("Alert ID", alert.Id.ToString()[..8], inline: true)
                .AddField("Type", alert.AlertType, inline: true)
                .AddField("City", city, inline: true)
                .AddField("Dates", $"{checkInDate:MMM dd} - {checkOutDate:MMM dd, yyyy}", inline: true)
                .AddField("Max Price", $"${maxPrice}/night", inline: true)
                .AddField("Frequency", frequency, inline: true)
                .WithFooter("Use /alert-list to view all your alerts")
                .WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(hotelName))
            {
                embedBuilder.AddField("Hotel", hotelName, inline: false);
            }

            await FollowupAsync(embed: embedBuilder.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert for user {UserId}", Context.User.Id);
            await FollowupAsync("An error occurred while creating your alert. Please try again.", ephemeral: true);
        }
    }

    [SlashCommand("alert-list", "List all your price alerts")]
    public async Task ListAlertsAsync(
        [Summary("show-inactive", "Include paused alerts")] bool showInactive = false)
    {
        try
        {
            var discordId = (long)Context.User.Id;
            var alerts = await _alertRepository.GetByUserIdAsync(discordId, activeOnly: !showInactive);
            var alertList = alerts.ToList();

            if (alertList.Count == 0)
            {
                await FollowupAsync("You don't have any alerts yet. Use `/alert-create` to create one!", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle($"Your Alerts ({alertList.Count})")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            foreach (var alert in alertList.Take(10))
            {
                var status = alert.IsActive ? "Active" : "Paused";
                var location = string.IsNullOrEmpty(alert.HotelName) ? alert.City : $"{alert.HotelName}, {alert.City}";

                embed.AddField(
                    $"{alert.Id.ToString()[..8]} - {location}",
                    $"**Dates:** {alert.CheckIn:MMM dd} - {alert.CheckOut:MMM dd}\n" +
                    $"**Max:** ${alert.MaxPrice}/night | **Status:** {status}",
                    inline: false);
            }

            if (alertList.Count > 10)
            {
                embed.WithFooter($"Showing 10 of {alertList.Count} alerts");
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing alerts for user {UserId}", Context.User.Id);
            await FollowupAsync("An error occurred while fetching your alerts.", ephemeral: true);
        }
    }

    [SlashCommand("alert-view", "View details of a specific alert")]
    public async Task ViewAlertAsync(
        [Summary("id", "Alert ID (first 8 characters)")] string alertId)
    {
        try
        {
            var alert = await FindAlertByPartialIdAsync(alertId);

            if (alert == null)
            {
                await FollowupAsync("Alert not found. Use `/alert-list` to see your alerts.", ephemeral: true);
                return;
            }

            if (alert.UserId != (long)Context.User.Id)
            {
                await FollowupAsync("You can only view your own alerts.", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("Alert Details")
                .WithColor(alert.IsActive ? Color.Green : Color.Orange)
                .AddField("Alert ID", alert.Id.ToString(), inline: false)
                .AddField("Type", alert.AlertType, inline: true)
                .AddField("Status", alert.IsActive ? "Active" : "Paused", inline: true)
                .AddField("City", alert.City, inline: true)
                .AddField("Check-in", alert.CheckIn.ToString("MMM dd, yyyy"), inline: true)
                .AddField("Check-out", alert.CheckOut.ToString("MMM dd, yyyy"), inline: true)
                .AddField("Guests", alert.Guests.ToString(), inline: true)
                .AddField("Max Price", $"${alert.MaxPrice}/night", inline: true)
                .AddField("Frequency", alert.CheckFrequency, inline: true)
                .AddField("Times Triggered", alert.TimesTriggered.ToString(), inline: true)
                .AddField("Created", alert.CreatedAt.ToString("MMM dd, yyyy HH:mm"), inline: true)
                .WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(alert.HotelName))
            {
                embed.AddField("Hotel", alert.HotelName, inline: false);
            }

            if (alert.LastCheckedAt.HasValue)
            {
                embed.AddField("Last Checked", alert.LastCheckedAt.Value.ToString("MMM dd, yyyy HH:mm"), inline: true);
            }

            await FollowupAsync(embed: embed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error viewing alert {AlertId}", alertId);
            await FollowupAsync("An error occurred while fetching the alert.", ephemeral: true);
        }
    }

    [SlashCommand("alert-pause", "Pause an active alert")]
    public async Task PauseAlertAsync(
        [Summary("id", "Alert ID (first 8 characters)")] string alertId)
    {
        try
        {
            var alert = await FindAlertByPartialIdAsync(alertId);

            if (alert == null)
            {
                await FollowupAsync("Alert not found.", ephemeral: true);
                return;
            }

            if (alert.UserId != (long)Context.User.Id)
            {
                await FollowupAsync("You can only pause your own alerts.", ephemeral: true);
                return;
            }

            if (!alert.IsActive)
            {
                await FollowupAsync("This alert is already paused.", ephemeral: true);
                return;
            }

            alert.IsActive = false;
            await _alertRepository.UpdateAsync(alert);

            _logger.LogInformation("Alert {AlertId} paused by user {UserId}", alert.Id, Context.User.Id);

            await FollowupAsync($"Alert `{alertId}` has been paused. Use `/alert-resume` to reactivate it.", ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing alert {AlertId}", alertId);
            await FollowupAsync("An error occurred while pausing the alert.", ephemeral: true);
        }
    }

    [SlashCommand("alert-resume", "Resume a paused alert")]
    public async Task ResumeAlertAsync(
        [Summary("id", "Alert ID (first 8 characters)")] string alertId)
    {
        try
        {
            var alert = await FindAlertByPartialIdAsync(alertId);

            if (alert == null)
            {
                await FollowupAsync("Alert not found.", ephemeral: true);
                return;
            }

            if (alert.UserId != (long)Context.User.Id)
            {
                await FollowupAsync("You can only resume your own alerts.", ephemeral: true);
                return;
            }

            if (alert.IsActive)
            {
                await FollowupAsync("This alert is already active.", ephemeral: true);
                return;
            }

            alert.IsActive = true;
            alert.NextCheckAt = DateTime.UtcNow;
            await _alertRepository.UpdateAsync(alert);

            _logger.LogInformation("Alert {AlertId} resumed by user {UserId}", alert.Id, Context.User.Id);

            await FollowupAsync($"Alert `{alertId}` has been resumed and will be checked soon.", ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming alert {AlertId}", alertId);
            await FollowupAsync("An error occurred while resuming the alert.", ephemeral: true);
        }
    }

    [SlashCommand("alert-delete", "Delete an alert permanently")]
    public async Task DeleteAlertAsync(
        [Summary("id", "Alert ID (first 8 characters)")] string alertId)
    {
        try
        {
            var alert = await FindAlertByPartialIdAsync(alertId);

            if (alert == null)
            {
                await FollowupAsync("Alert not found.", ephemeral: true);
                return;
            }

            if (alert.UserId != (long)Context.User.Id)
            {
                await FollowupAsync("You can only delete your own alerts.", ephemeral: true);
                return;
            }

            await _alertRepository.DeleteAsync(alert.Id);

            _logger.LogInformation("Alert {AlertId} deleted by user {UserId}", alert.Id, Context.User.Id);

            await FollowupAsync($"Alert `{alertId}` has been permanently deleted.", ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting alert {AlertId}", alertId);
            await FollowupAsync("An error occurred while deleting the alert.", ephemeral: true);
        }
    }

    /// <summary>
    /// Finds an alert by partial ID (first 8 characters of GUID)
    /// </summary>
    private async Task<Alert?> FindAlertByPartialIdAsync(string partialId)
    {
        // Try full GUID first
        if (Guid.TryParse(partialId, out var fullGuid))
        {
            return await _alertRepository.GetByIdAsync(fullGuid);
        }

        // Search by partial ID - get user's alerts and match prefix
        var discordId = (long)Context.User.Id;
        var alerts = await _alertRepository.GetByUserIdAsync(discordId, activeOnly: false);

        return alerts.FirstOrDefault(a =>
            a.Id.ToString().StartsWith(partialId, StringComparison.OrdinalIgnoreCase));
    }
}
