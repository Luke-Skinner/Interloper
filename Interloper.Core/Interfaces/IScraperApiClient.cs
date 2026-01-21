namespace Interloper.Core.Interfaces;

/// <summary>
/// Client for communicating with the Python scraper API
/// </summary>
public interface IScraperApiClient
{
    /// <summary>
    /// Triggers a hotel scrape
    /// </summary>
    Task<ScraperResponse> ScrapeHotelAsync(ScraperRequest request);

    /// <summary>
    /// Triggers a city-wide scrape
    /// </summary>
    Task<ScraperResponse> ScrapeCityAsync(ScraperRequest request);

    /// <summary>
    /// Checks the health of the scraper API
    /// </summary>
    Task<bool> HealthCheckAsync();
}

/// <summary>
/// Request to the scraper API
/// </summary>
public class ScraperRequest
{
    public string? HotelName { get; set; }
    public string City { get; set; } = string.Empty;
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int Guests { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinRating { get; set; }
    public List<string>? Amenities { get; set; }
    public bool FreeCancellation { get; set; }
    public List<string>? PropertyTypes { get; set; }
    public List<string>? Platforms { get; set; }
}

/// <summary>
/// Response from the scraper API
/// </summary>
public class ScraperResponse
{
    public bool Success { get; set; }
    public List<HotelResult> Hotels { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Individual hotel result from scraper
/// </summary>
public class HotelResult
{
    public string Platform { get; set; } = string.Empty;
    public string HotelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? TotalPrice { get; set; }
    public decimal? Rating { get; set; }
    public int? ReviewCount { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public List<string>? Amenities { get; set; }
    public string? CancellationPolicy { get; set; }
    public string? BookingUrl { get; set; }
}
