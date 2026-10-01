namespace Nuntiator.SampleApi.Features.Orders;

public record CreateOrderCommand(string CustomerName, string ItemName, int Quantity, decimal UnitPrice) : ICommand<OrderResponse>;

public record OrderResponse(Guid OrderId, string CustomerName, string ItemName, int Quantity, decimal TotalPrice, DateTime CreatedAt);

public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, OrderResponse>
{
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(ILogger<CreateOrderCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task<OrderResponse> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing order for {CustomerName}: {Quantity}x {ItemName}", command.CustomerName, command.Quantity, command.ItemName);

        // Simulate async DB or business logic delay
        await Task.Delay(50, cancellationToken);

        var orderId = Guid.NewGuid();
        var totalPrice = command.Quantity * command.UnitPrice;

        return new OrderResponse(
            OrderId: orderId,
            CustomerName: command.CustomerName,
            ItemName: command.ItemName,
            Quantity: command.Quantity,
            TotalPrice: totalPrice,
            CreatedAt: DateTime.UtcNow
        );
    }
}
