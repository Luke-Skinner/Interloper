using Discord;
using Discord.WebSocket;
using Interloper.Core.Models;
using Microsoft.Extensions.Logging;

namespace Interloper.Discord.Services;

// Service for sending Discord DM notifications to users
public class NotificationService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        DiscordSocketClient client,
        ILogger<NotificationService> logger)
    {
        _client = client;
        _logger = logger;
    }

    // Sends a price alert notification to a user via DM
    public async Task<bool> SendPriceAlertAsync(
        long discordUserId,
        Alert alert,
        string hotelName,
        decimal currentPrice,
        string platform,
        string? bookingUrl = null)
    {
        try
        {
            var user = await _client.GetUserAsync((ulong)discordUserId);
            if (user == null)
            {
                _logger.LogWarning("Could not find Discord user {UserId}", discordUserId);
                return false;
            }

            var dmChannel = await user.CreateDMChannelAsync();

            var embed = new EmbedBuilder()
                .WithTitle("Price Alert Triggered!")
                .WithColor(Color.Gold)
                .WithDescription($"A hotel matching your alert is now below your target price.")
                .AddField("Hotel", hotelName, inline: false)
                .AddField("City", alert.City, inline: true)
                .AddField("Platform", platform, inline: true)
                .AddField("Current Price", $"${currentPrice}/night", inline: true)
                .AddField("Your Max Price", $"${alert.MaxPrice}/night", inline: true)
                .AddField("Dates", $"{alert.CheckIn:MMM dd} - {alert.CheckOut:MMM dd, yyyy}", inline: true)
                .AddField("Guests", alert.Guests.ToString(), inline: true)
                .WithFooter($"Alert ID: {alert.Id.ToString()[..8]}")
                .WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(bookingUrl))
            {
                embed.WithUrl(bookingUrl);
                embed.AddField("Book Now", $"[View Deal]({bookingUrl})", inline: false);
            }

            await dmChannel.SendMessageAsync(embed: embed.Build());

            _logger.LogInformation(
                "Sent price alert to user {UserId} for alert {AlertId}: {Hotel} at ${Price}",
                discordUserId, alert.Id, hotelName, currentPrice);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send DM to user {UserId}", discordUserId);
            return false;
        }
    }

    // Sends a summary notification with multiple deals
    public async Task<bool> SendDealsSummaryAsync(
        long discordUserId,
        Alert alert,
        List<HotelDeal> deals)
    {
        try
        {
            if (deals.Count == 0) return true;

            var user = await _client.GetUserAsync((ulong)discordUserId);
            if (user == null)
            {
                _logger.LogWarning("Could not find Discord user {UserId}", discordUserId);
                return false;
            }

            var dmChannel = await user.CreateDMChannelAsync();

            var embed = new EmbedBuilder()
                .WithTitle($"Found {deals.Count} Deal(s) in {alert.City}!")
                .WithColor(Color.Green)
                .WithDescription($"Hotels below your ${alert.MaxPrice}/night target for {alert.CheckIn:MMM dd} - {alert.CheckOut:MMM dd}")
                .WithFooter($"Alert ID: {alert.Id.ToString()[..8]}")
                .WithCurrentTimestamp();

            foreach (var deal in deals.Take(5))
            {
                var fieldValue = $"**${deal.Price}/night** on {deal.Platform}";
                if (deal.Rating.HasValue)
                {
                    fieldValue += $" | Rating: {deal.Rating:F1}/5";
                }
                embed.AddField(deal.HotelName, fieldValue, inline: false);
            }

            if (deals.Count > 5)
            {
                embed.AddField($"+ {deals.Count - 5} more deals", "Use `/alert-view` for full details", inline: false);
            }

            await dmChannel.SendMessageAsync(embed: embed.Build());

            _logger.LogInformation(
                "Sent deals summary to user {UserId} for alert {AlertId}: {Count} deals",
                discordUserId, alert.Id, deals.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send deals summary to user {UserId}", discordUserId);
            return false;
        }
    }
}

// DTO for hotel deal information
public class HotelDeal
{
    public required string HotelName { get; set; }
    public decimal Price { get; set; }
    public required string Platform { get; set; }
    public decimal? Rating { get; set; }
    public string? BookingUrl { get; set; }
}
