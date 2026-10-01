using Nuntiator;
using Nuntiator.SampleApi.Features.Orders;
using Nuntiator.SampleApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Register OpenAPI / Swagger
builder.Services.AddOpenApi();

// Register Nuntiator with automatic assembly scanning and middleware pipeline
builder.Services.AddNuntiator(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenMiddleware(typeof(LoggingMiddleware<,>));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// -------------------------------------------------------------
// Endpoints demonstrating Nuntiator usage
// -------------------------------------------------------------

// 1. Send Command with Response (Create Order)
app.MapPost("/api/orders", async (CreateOrderCommand command, INuntiator nuntiator) =>
{
    var response = await nuntiator.SendAsync(command);
    return Results.Created($"/api/orders/{response.OrderId}", response);
})
.WithName("CreateOrder")
.WithSummary("Creates a new order using Nuntiator command handler");

// 2. Send Void Command without Response (Cancel Order)
app.MapPost("/api/orders/{id:guid}/cancel", async (Guid id, string? reason, INuntiator nuntiator) =>
{
    var command = new CancelOrderCommand(id, reason ?? "No reason provided");
    await nuntiator.SendAsync(command);
    return Results.NoContent();
})
.WithName("CancelOrder")
.WithSummary("Cancels an existing order using Nuntiator void command handler");

app.Run();

// Make Program class accessible to WebApplicationFactory if tests are added later
public partial class Program { }
