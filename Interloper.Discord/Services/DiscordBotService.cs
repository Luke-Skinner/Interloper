using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Interloper.Discord.Services;

/// <summary>
/// Hosted service that manages the Discord bot lifecycle
/// </summary>
public class DiscordBotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly InteractionHandler _interactionHandler;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordBotService> _logger;

    public DiscordBotService(
        DiscordSocketClient client,
        InteractionService interactions,
        InteractionHandler interactionHandler,
        IConfiguration configuration,
        ILogger<DiscordBotService> logger)
    {
        _client = client;
        _interactions = interactions;
        _interactionHandler = interactionHandler;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Discord bot service");

        // Set up event handlers
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;

        // Get bot token from environment variable or configuration
        var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN")
            ?? _configuration["Discord:BotToken"]
            ?? throw new InvalidOperationException("Discord bot token not found. Set DISCORD_BOT_TOKEN environment variable.");

        // Initialize interaction handler
        await _interactionHandler.InitializeAsync();

        // Login and start
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _logger.LogInformation("Discord bot service started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Discord bot service");
        await _client.StopAsync();
        await _client.LogoutAsync();
        _logger.LogInformation("Discord bot service stopped");
    }

    private Task LogAsync(LogMessage message)
    {
        var severity = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        _logger.Log(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
        _logger.LogInformation("Bot is connected and ready!");
        _logger.LogInformation("Connected as {Username}#{Discriminator}", _client.CurrentUser.Username, _client.CurrentUser.Discriminator);

        // Register slash commands
        try
        {
            // For development: Register commands to a specific guild for instant updates
            // For production: Use RegisterCommandsGloballyAsync() instead
            var guildId = Environment.GetEnvironmentVariable("DISCORD_GUILD_ID");

            if (!string.IsNullOrEmpty(guildId) && ulong.TryParse(guildId, out var id))
            {
                _logger.LogInformation("Registering commands to guild {GuildId}", guildId);
                await _interactions.RegisterCommandsToGuildAsync(id);
            }
            else
            {
                _logger.LogInformation("Registering commands globally");
                await _interactions.RegisterCommandsGloballyAsync();
            }

            _logger.LogInformation("Slash commands registered successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register slash commands");
        }
    }
}
