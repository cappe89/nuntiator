namespace Nuntiator.SampleApi.Features.Orders;

public record CancelOrderCommand(Guid OrderId, string Reason) : ICommand;

public class CancelOrderCommandHandler : ICommandHandler<CancelOrderCommand>
{
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(ILogger<CancelOrderCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(CancelOrderCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cancelling order {OrderId}. Reason: {Reason}", command.OrderId, command.Reason);

        // Simulate async operation
        await Task.Delay(30, cancellationToken);
    }
}
