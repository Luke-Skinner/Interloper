using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Interloper.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Interloper.Discord.Services;

// HTTP client for communicating with the Python scraper API
public class ScraperApiClient : IScraperApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ScraperApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ScraperApiClient(HttpClient httpClient, ILogger<ScraperApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public async Task<ScraperResponse> ScrapeHotelAsync(ScraperRequest request)
    {
        return await SearchAsync(request);
    }

    public async Task<ScraperResponse> ScrapeCityAsync(ScraperRequest request)
    {
        return await SearchAsync(request);
    }

    private async Task<ScraperResponse> SearchAsync(ScraperRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Calling scraper API for {City}, {CheckIn} to {CheckOut}",
                request.City, request.CheckIn, request.CheckOut);

            var apiRequest = new ScraperApiRequest
            {
                City = request.City,
                CheckIn = request.CheckIn.ToString("yyyy-MM-dd"),
                CheckOut = request.CheckOut.ToString("yyyy-MM-dd"),
                Guests = request.Guests,
                HotelName = request.HotelName,
                MaxPrice = request.MaxPrice,
                MinRating = request.MinRating,
                FreeCancellation = request.FreeCancellation,
                Platforms = request.Platforms,
            };

            var response = await _httpClient.PostAsJsonAsync("/search", apiRequest, _jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Scraper API error: {StatusCode} - {Content}",
                    response.StatusCode, errorContent);

                return new ScraperResponse
                {
                    Success = false,
                    ErrorMessage = $"API returned {response.StatusCode}"
                };
            }

            var apiResponse = await response.Content.ReadFromJsonAsync<ScraperApiResponse>(_jsonOptions);

            if (apiResponse == null)
            {
                return new ScraperResponse
                {
                    Success = false,
                    ErrorMessage = "Failed to deserialize response"
                };
            }

            _logger.LogInformation("Scraper returned {Count} hotels", apiResponse.Hotels?.Count ?? 0);

            return new ScraperResponse
            {
                Success = apiResponse.Success,
                Hotels = apiResponse.Hotels?.Select(h => new HotelResult
                {
                    Platform = h.Platform ?? "",
                    HotelId = h.HotelId ?? "",
                    Name = h.Name ?? "",
                    Price = h.Price,
                    TotalPrice = h.TotalPrice,
                    Rating = h.Rating,
                    ReviewCount = h.ReviewCount,
                    Address = h.Address,
                    City = h.City,
                    Amenities = h.Amenities,
                    CancellationPolicy = h.CancellationPolicy,
                    BookingUrl = h.BookingUrl,
                }).ToList() ?? new List<HotelResult>(),
                ErrorMessage = apiResponse.ErrorMessage,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling scraper API");
            return new ScraperResponse
            {
                Success = false,
                ErrorMessage = $"Connection error: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout calling scraper API");
            return new ScraperResponse
            {
                Success = false,
                ErrorMessage = "Request timed out"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling scraper API");
            return new ScraperResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scraper health check failed");
            return false;
        }
    }
}

// Internal DTOs for API communication (snake_case)
internal class ScraperApiRequest
{
    public string City { get; set; } = "";
    public string CheckIn { get; set; } = "";
    public string CheckOut { get; set; } = "";
    public int Guests { get; set; }
    public string? HotelName { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinRating { get; set; }
    public bool FreeCancellation { get; set; }
    public List<string>? Platforms { get; set; }
}

internal class ScraperApiResponse
{
    public bool Success { get; set; }
    public List<ScraperApiHotel>? Hotels { get; set; }
    public int TotalResults { get; set; }
    public List<string>? PlatformsSearched { get; set; }
    public string? ErrorMessage { get; set; }
}

internal class ScraperApiHotel
{
    public string? Platform { get; set; }
    public string? HotelId { get; set; }
    public string? Name { get; set; }
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
