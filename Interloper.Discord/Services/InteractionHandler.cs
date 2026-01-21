using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Interloper.Discord.Services;

/// <summary>
/// Handles Discord slash command interactions
/// </summary>
public class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly ILogger<InteractionHandler> _logger;

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService interactions,
        IServiceProvider services,
        ILogger<InteractionHandler> logger)
    {
        _client = client;
        _interactions = interactions;
        _services = services;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        // Add command modules
        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        // Set up event handlers
        _client.InteractionCreated += HandleInteraction;
        _interactions.SlashCommandExecuted += SlashCommandExecuted;
        _interactions.Log += LogAsync;

        _logger.LogInformation("Interaction handler initialized");
    }

    private Task LogAsync(LogMessage log)
    {
        if (log.Exception != null)
        {
            _logger.LogError(log.Exception, "InteractionService error: {Message}", log.Message);
        }
        else
        {
            _logger.LogInformation("InteractionService: {Message}", log.Message);
        }
        return Task.CompletedTask;
    }

    private Task HandleInteraction(SocketInteraction interaction)
    {
        // Log timing immediately when event is received
        var receivedAt = DateTimeOffset.UtcNow;
        var interactionAge = receivedAt - interaction.CreatedAt;
        _logger.LogInformation("Interaction received. Created: {Created}, Received: {Received}, Age: {Age}ms",
            interaction.CreatedAt, receivedAt, interactionAge.TotalMilliseconds);

        // Fire-and-forget to avoid blocking the gateway thread
        // This ensures the defer happens as fast as possible
        _ = Task.Run(async () =>
        {
            try
            {
                var deferStart = DateTimeOffset.UtcNow;
                _logger.LogInformation("About to defer. Time since creation: {Age}ms",
                    (deferStart - interaction.CreatedAt).TotalMilliseconds);

                // Defer immediately to avoid 3-second timeout
                if (interaction is SocketSlashCommand)
                {
                    await interaction.DeferAsync(ephemeral: true);
                    _logger.LogInformation("Defer completed successfully");
                }

                var context = new SocketInteractionContext(_client, interaction);

                // Use root service provider - Discord.NET will create its own scope internally
                var result = await _interactions.ExecuteCommandAsync(context, _services);

                if (result is ExecuteResult executeResult && executeResult.Exception != null)
                {
                    _logger.LogError(executeResult.Exception, "Command threw exception");
                }

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Command failed: {Error}", result.ErrorReason);

                    if (!interaction.HasResponded)
                    {
                        await interaction.FollowupAsync($"Error: {result.ErrorReason}", ephemeral: true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception handling interaction: {Message}", ex.Message);

                try
                {
                    if (interaction.HasResponded)
                    {
                        await interaction.FollowupAsync("An error occurred.", ephemeral: true);
                    }
                    else
                    {
                        await interaction.RespondAsync("An error occurred.", ephemeral: true);
                    }
                }
                catch { }
            }
        });

        return Task.CompletedTask;
    }

    private Task SlashCommandExecuted(SlashCommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Command {CommandName} failed for user {User}: {Error}",
                commandInfo.Name,
                context.User.Username,
                result.ErrorReason);
        }
        else
        {
            _logger.LogInformation("Command {CommandName} executed successfully for user {User}",
                commandInfo.Name,
                context.User.Username);
        }

        return Task.CompletedTask;
    }
}
