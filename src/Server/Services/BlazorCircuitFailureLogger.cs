using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Server.Services;

/// <summary>
/// Logs Blazor Server circuit failures so production disconnects are visible in container/app logs
/// (the browser only shows a generic WebSocket close).
/// </summary>
public sealed class BlazorCircuitFailureLogger(ILogger<BlazorCircuitFailureLogger> logger) : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
        => async context =>
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Blazor circuit inbound activity failed (circuit {CircuitId})",
                    context.Circuit.Id);
                throw;
            }
        };

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogDebug("Blazor circuit {CircuitId} connection down", circuit.Id);
        return Task.CompletedTask;
    }
}
