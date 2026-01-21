using Interloper.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Interloper.Data;

/// <summary>
/// Database context for the Interloper bot
/// </summary>
public class InterloperDbContext : DbContext
{
    public InterloperDbContext(DbContextOptions<InterloperDbContext> options)
        : base(options)
    {
    }

    // DbSets for all entities
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Alert> Alerts { get; set; } = null!;
    public DbSet<Hotel> Hotels { get; set; } = null!;
    public DbSet<PriceHistory> PriceHistories { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<ScraperJob> ScraperJobs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.DiscordId);

            entity.Property(e => e.DiscordId).HasColumnName("discord_id");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Tier).HasColumnName("tier").HasMaxLength(50).HasDefaultValue("free");
            entity.Property(e => e.AlertCount).HasColumnName("alert_count").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.LastActive).HasColumnName("last_active").HasDefaultValueSql("NOW()");

            // Relationships
            entity.HasMany(e => e.Alerts)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Notifications)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Alert entity
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("alerts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.AlertType).HasColumnName("alert_type").HasMaxLength(50).IsRequired();

            // Search criteria
            entity.Property(e => e.HotelName).HasColumnName("hotel_name").HasMaxLength(255);
            entity.Property(e => e.City).HasColumnName("city").HasMaxLength(255).IsRequired();
            entity.Property(e => e.CheckIn).HasColumnName("check_in").IsRequired();
            entity.Property(e => e.CheckOut).HasColumnName("check_out").IsRequired();
            entity.Property(e => e.Guests).HasColumnName("guests").IsRequired();

            // Thresholds
            entity.Property(e => e.MaxPrice).HasColumnName("max_price").HasPrecision(10, 2).IsRequired();
            entity.Property(e => e.MinRating).HasColumnName("min_rating").HasPrecision(3, 2).HasDefaultValue(0);

            // Filters (JSON columns)
            entity.Property(e => e.RequiredAmenities).HasColumnName("required_amenities").HasColumnType("jsonb");
            entity.Property(e => e.FreeCancellation).HasColumnName("free_cancellation").HasDefaultValue(false);
            entity.Property(e => e.PropertyTypes).HasColumnName("property_types").HasColumnType("jsonb");

            // Scheduling
            entity.Property(e => e.CheckFrequency).HasColumnName("check_frequency").HasMaxLength(20).IsRequired();
            entity.Property(e => e.LastCheckedAt).HasColumnName("last_checked_at");
            entity.Property(e => e.NextCheckAt).HasColumnName("next_check_at");

            // Status
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.TimesTriggered).HasColumnName("times_triggered").HasDefaultValue(0);

            // Metadata
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            // Indexes
            entity.HasIndex(e => new { e.NextCheckAt, e.IsActive }).HasDatabaseName("idx_alerts_next_check");
            entity.HasIndex(e => new { e.UserId, e.IsActive }).HasDatabaseName("idx_alerts_user");
        });

        // Configure Hotel entity
        modelBuilder.Entity<Hotel>(entity =>
        {
            entity.ToTable("hotels");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.PlatformHotelId).HasColumnName("platform_hotel_id").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Platform).HasColumnName("platform").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(500).IsRequired();
            entity.Property(e => e.City).HasColumnName("city").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Address).HasColumnName("address");

            // Location
            entity.Property(e => e.Latitude).HasColumnName("latitude").HasPrecision(10, 8);
            entity.Property(e => e.Longitude).HasColumnName("longitude").HasPrecision(11, 8);
            entity.Property(e => e.DistanceToCenterKm).HasColumnName("distance_to_center_km").HasPrecision(5, 2);

            // Quality metrics
            entity.Property(e => e.Rating).HasColumnName("rating").HasPrecision(3, 2);
            entity.Property(e => e.ReviewCount).HasColumnName("review_count");
            entity.Property(e => e.StarRating).HasColumnName("star_rating");

            // Amenities (JSON)
            entity.Property(e => e.Amenities).HasColumnName("amenities").HasColumnType("jsonb");
            entity.Property(e => e.PropertyType).HasColumnName("property_type").HasMaxLength(100);

            // Metadata
            entity.Property(e => e.LastScrapedAt).HasColumnName("last_scraped_at").HasDefaultValueSql("NOW()");

            // Unique constraint
            entity.HasIndex(e => new { e.Platform, e.PlatformHotelId })
                .IsUnique()
                .HasDatabaseName("unique_platform_hotel");

            // Indexes
            entity.HasIndex(e => e.City).HasDatabaseName("idx_hotels_city");
            entity.HasIndex(e => e.Rating).HasDatabaseName("idx_hotels_rating");
        });

        // Configure PriceHistory entity
        modelBuilder.Entity<PriceHistory>(entity =>
        {
            entity.ToTable("price_history");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.HotelId).HasColumnName("hotel_id").IsRequired();
            entity.Property(e => e.AlertId).HasColumnName("alert_id").IsRequired();
            entity.Property(e => e.Platform).HasColumnName("platform").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Price).HasColumnName("price").HasPrecision(10, 2).IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("USD");

            // Booking details
            entity.Property(e => e.CheckInDate).HasColumnName("check_in_date").IsRequired();
            entity.Property(e => e.CheckoutDate).HasColumnName("checkout_date").IsRequired();
            entity.Property(e => e.Guests).HasColumnName("guests").IsRequired();
            entity.Property(e => e.RoomType).HasColumnName("room_type").HasMaxLength(255);

            // Additional pricing info
            entity.Property(e => e.TotalPrice).HasColumnName("total_price").HasPrecision(10, 2);
            entity.Property(e => e.CancellationPolicy).HasColumnName("cancellation_policy").HasMaxLength(255);
            entity.Property(e => e.BreakfastIncluded).HasColumnName("breakfast_included").HasDefaultValue(false);
            entity.Property(e => e.CheckedAt).HasColumnName("checked_at").HasDefaultValueSql("NOW()");

            // Relationships
            entity.HasOne(e => e.Hotel)
                .WithMany(e => e.PriceHistories)
                .HasForeignKey(e => e.HotelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Alert)
                .WithMany(e => e.PriceHistories)
                .HasForeignKey(e => e.AlertId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => new { e.HotelId, e.CheckedAt }).HasDatabaseName("idx_price_history_hotel");
            entity.HasIndex(e => new { e.AlertId, e.CheckedAt }).HasDatabaseName("idx_price_history_alert");
        });

        // Configure Notification entity
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.AlertId).HasColumnName("alert_id").IsRequired();
            entity.Property(e => e.HotelId).HasColumnName("hotel_id").IsRequired();
            entity.Property(e => e.NotificationType).HasColumnName("notification_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PriceAtNotification).HasColumnName("price_at_notification").HasPrecision(10, 2).IsRequired();
            entity.Property(e => e.SentAt).HasColumnName("sent_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UserClicked).HasColumnName("user_clicked").HasDefaultValue(false);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany(e => e.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Alert)
                .WithMany(e => e.Notifications)
                .HasForeignKey(e => e.AlertId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Hotel)
                .WithMany(e => e.Notifications)
                .HasForeignKey(e => e.HotelId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => new { e.UserId, e.SentAt }).HasDatabaseName("idx_notifications_user");
        });

        // Configure ScraperJob entity
        modelBuilder.Entity<ScraperJob>(entity =>
        {
            entity.ToTable("scraper_jobs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AlertId).HasColumnName("alert_id").IsRequired();
            entity.Property(e => e.JobType).HasColumnName("job_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.SearchParams).HasColumnName("search_params").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("pending");
            entity.Property(e => e.Priority).HasColumnName("priority").HasDefaultValue(5);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.MaxRetries).HasColumnName("max_retries").HasDefaultValue(3);

            // Relationship
            entity.HasOne(e => e.Alert)
                .WithMany(e => e.ScraperJobs)
                .HasForeignKey(e => e.AlertId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => new { e.Status, e.Priority, e.CreatedAt }).HasDatabaseName("idx_scraper_jobs_status");
        });
    }
}
