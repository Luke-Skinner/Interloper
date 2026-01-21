using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Interloper.Data;

/// <summary>
/// Design-time factory for creating DbContext instances during migrations
/// </summary>
public class InterloperDbContextFactory : IDesignTimeDbContextFactory<InterloperDbContext>
{
    public InterloperDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InterloperDbContext>();

        // Use connection string from environment variable or default for local development
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5433;Database=hotelbot;Username=postgres;Password=postgres123";

        optionsBuilder.UseNpgsql(connectionString);

        return new InterloperDbContext(optionsBuilder.Options);
    }
}
