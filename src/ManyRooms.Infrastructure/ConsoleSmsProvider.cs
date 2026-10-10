using ManyRooms.Application;
using Microsoft.Extensions.Logging;

namespace ManyRooms.Infrastructure;

/// <summary>Development-only placeholder. Does not deliver SMS or log phone numbers/message contents.</summary>
public sealed class ConsoleSmsProvider(ILogger<ConsoleSmsProvider> logger) : ISmsProvider
{
    public Task SendAsync(string recipient, string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Demo SMS suppressed; no provider is connected.");
        return Task.CompletedTask;
    }
}
