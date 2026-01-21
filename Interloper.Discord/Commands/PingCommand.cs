using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace Interloper.Discord.Commands;

/// <summary>
/// Simple ping command to test bot connectivity
/// </summary>
public class PingCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<PingCommand> _logger;

    public PingCommand(ILogger<PingCommand> logger)
    {
        _logger = logger;
    }

    [SlashCommand("ping", "Check if the bot is responsive")]
    public async Task HandlePingAsync()
    {
        _logger.LogInformation("Ping command executed by {User}", Context.User.Username);

        var embed = new EmbedBuilder()
            .WithTitle("Pong!")
            .WithDescription("Interloper bot is online and responsive.")
            .WithColor(Color.Green)
            .AddField("Latency", $"{Context.Client.Latency}ms", inline: true)
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: embed);
    }
}
