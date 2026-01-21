namespace Interloper.Core.Models;

/// <summary>
/// Represents a price check record for a hotel
/// </summary>
public class PriceHistory
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Hotel this price check is for
    /// </summary>
    public Guid HotelId { get; set; }

    /// <summary>
    /// Alert that triggered this price check
    /// </summary>
    public Guid AlertId { get; set; }

    /// <summary>
    /// Platform where the price was found
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Price per night
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Currency code (e.g., 'USD', 'EUR')
    /// </summary>
    public string Currency { get; set; } = "USD";

    // Booking Details
    /// <summary>
    /// Check-in date for this price
    /// </summary>
    public DateOnly CheckInDate { get; set; }

    /// <summary>
    /// Check-out date for this price
    /// </summary>
    public DateOnly CheckoutDate { get; set; }

    /// <summary>
    /// Number of guests for this price
    /// </summary>
    public int Guests { get; set; }

    /// <summary>
    /// Room type (e.g., 'Standard Double Room')
    /// </summary>
    public string? RoomType { get; set; }

    // Additional Pricing Info
    /// <summary>
    /// Total price for the entire stay
    /// </summary>
    public decimal? TotalPrice { get; set; }

    /// <summary>
    /// Cancellation policy description
    /// </summary>
    public string? CancellationPolicy { get; set; }

    /// <summary>
    /// Whether breakfast is included
    /// </summary>
    public bool BreakfastIncluded { get; set; } = false;

    /// <summary>
    /// When this price was checked
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    /// <summary>
    /// The hotel this price is for
    /// </summary>
    public Hotel Hotel { get; set; } = null!;

    /// <summary>
    /// The alert that triggered this price check
    /// </summary>
    public Alert Alert { get; set; } = null!;
}
