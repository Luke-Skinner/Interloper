namespace Interloper.Core.Models;

/// <summary>
/// Represents cached hotel information from various platforms
/// </summary>
public class Hotel
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Hotel ID from the platform (e.g., Booking.com hotel ID)
    /// </summary>
    public string PlatformHotelId { get; set; } = string.Empty;

    /// <summary>
    /// Platform source (e.g., 'booking', 'hotels_com', 'airbnb')
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Hotel name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// City where the hotel is located
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Street address
    /// </summary>
    public string? Address { get; set; }

    // Location
    /// <summary>
    /// Latitude coordinate
    /// </summary>
    public decimal? Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate
    /// </summary>
    public decimal? Longitude { get; set; }

    /// <summary>
    /// Distance to city center in kilometers
    /// </summary>
    public decimal? DistanceToCenterKm { get; set; }

    // Quality Metrics
    /// <summary>
    /// Overall rating (0-5 or platform-specific scale)
    /// </summary>
    public decimal? Rating { get; set; }

    /// <summary>
    /// Number of reviews
    /// </summary>
    public int? ReviewCount { get; set; }

    /// <summary>
    /// Star rating (1-5 stars)
    /// </summary>
    public int? StarRating { get; set; }

    // Amenities and Details (stored as JSON in database)
    /// <summary>
    /// Available amenities (e.g., ["wifi", "pool", "gym"])
    /// </summary>
    public List<string>? Amenities { get; set; }

    /// <summary>
    /// Property type (e.g., 'hotel', 'apartment', 'vacation_rental')
    /// </summary>
    public string? PropertyType { get; set; }

    // Metadata
    /// <summary>
    /// Last time this hotel data was scraped
    /// </summary>
    public DateTime LastScrapedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    /// <summary>
    /// Price history records for this hotel
    /// </summary>
    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

    /// <summary>
    /// Notifications related to this hotel
    /// </summary>
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
